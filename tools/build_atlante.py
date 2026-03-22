#!/usr/bin/env python3
"""
build_atlante.py — Genera il database Atlante (italy.db) dai CSV ISTAT open data.

Sorgenti:
  - ISTAT: Comuni, Variazioni storiche
  - GeoNames: CAP + coordinate WGS84
  - Banca d'Italia: Codici ABI/BIC banche
  - ISTAT: Codici ATECO 2007
  - IndicePA: Codici SdI per PA (iPA)
  - Zone climatiche: da CSV GitHub (DPR 412/93)
  - Zone sismiche: INGV/Protezione Civile per comune

Utilizzo:
    python tools/build_atlante.py [--output src/Italy.Core/data/italy.db] [--offline]
"""

import argparse
import io
import json
import logging
import sqlite3
import sys
import zipfile
from datetime import datetime
from pathlib import Path

import chardet
import pandas as pd
import requests
from tenacity import (
    retry,
    stop_after_attempt,
    wait_exponential,
    retry_if_exception_type,
    before_sleep_log,
)
from tqdm import tqdm

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
log = logging.getLogger(__name__)

# ── URL Fonti ─────────────────────────────────────────────────────────────────

ISTAT_COMUNI_URL = (
    "https://www.istat.it/storage/codici-unita-amministrative/"
    "Elenco-comuni-italiani.csv"
)
ISTAT_VARIAZIONI_URL = (
    "https://www.istat.it/storage/codici-unita-amministrative/"
    "Variazioni-amministrative-e-territoriali-dal-1991.zip"
)
GEONAMES_URL      = "https://download.geonames.org/export/zip/IT.zip"
# Banca d'Italia — Albo banche e intermediari (CSV open data)
BANCA_ITALIA_URL  = (
    "https://www.bancaditalia.it/compiti/vigilanza/intermediari/albo-banche-gruppi/"
    "albo-banche.csv"
)
# Banca d'Italia — Codici BIC (CSV separato)
BANCA_ITALIA_BIC_URL = (
    "https://www.bancaditalia.it/compiti/vigilanza/intermediari/albo-banche-gruppi/"
    "codici-bic.csv"
)
# ATECO 2007 aggiornamento 2022 — ISTAT ufficiale
ATECO_XLSX_URL    = (
    "https://www.istat.it/it/files/2022/03/"
    "Struttura-ATECO-2007-aggiornamento-2022.xlsx"
)
# IndicePA open data — XLSX ufficiale
IPA_XLSX_URL      = (
    "https://indicepa.gov.it/ipa-dati/dataset/"
    "5baa3eb8-266e-455a-8de8-b1f434c279b2/resource/"
    "d09adf99-dc10-4349-8c53-27b1e5aa97b6/download/enti.xlsx"
)
# Zone climatiche DPR 412/93 — non esiste CSV istituzionale open data,
# usiamo dataset GitHub verificato
ZONE_CLIMATICHE_URL = (
    "http://www.solaritaly.enea.it/clisun/Dati/FilesCSV/GraGioWb.txt"
)
# Zone sismiche — GitHub ferdi2005 (dati Protezione Civile, CC BY 4.0)
ZONE_SISMICHE_URL = (
    "https://raw.githubusercontent.com/ferdi2005/zonasismica/master/"
    "classificazione2024.csv"
)
# Aree Interne ISTAT — classificazione comuni (A=Centro, B=Cintura, C=Intermedio,
# D=Periferico, E=Ultraperiferico); fallback ZIP se CSV diretto non disponibile
AREE_INTERNE_URL = (
    "https://www.istat.it/storage/classificazioni/"
    "classificazione_comuni_aree_interne.csv"
)
AREE_INTERNE_ZIP_URL = (
    "https://www.istat.it/it/files/2023/12/"
    "Classificazione-comuni-aree-interne-2023-2025.zip"
)
# Popolazione ISTAT — denominazione, superficie, popolazione da censimento
POPOLAZIONE_URL = (
    "https://www.istat.it/storage/stato-avanzamento-lavori/comune/"
    "comuni-denominazione-superficie-popolazione-censimento.csv"
)
# ASL Ministero della Salute — elenco nazionale ASL per comune/provincia
ASL_URL = (
    "https://www.salute.gov.it/portale/temi/documenti/asl_nazionale.xlsx"
)
ASL_CSV_FALLBACK_URL = (
    "https://www.dati.salute.gov.it/imgs/C_17_dataset_21_download_itemDownload0_upFile.csv"
)
# ISPRA Catasto Rifiuti — produzione e raccolta differenziata su scala comunale
ISPRA_RIFIUTI_ANNO  = 2024
ISPRA_RIFIUTI_URL   = (
    f"https://www.catasto-rifiuti.isprambiente.it/get/"
    f"getDettaglioComunale.csv.php?aa={ISPRA_RIFIUTI_ANNO}"
)
# ARERA — Dati TARI e gestori rifiuti per comune (aggiornamento annuale)
ARERA_TARI_ANNO     = 2023
ARERA_TARI_URL      = (
    f"https://www.arera.it/fileadmin/allegati/dati/tari/tari_{ARERA_TARI_ANNO}.csv"
)
# MIMIT — Anagrafica impianti di distribuzione carburanti (aggiornamento mensile)
MIMIT_IMPIANTI_URL  = (
    "https://www.mimit.gov.it/images/exportCSV/anagrafica_impianti_attivi.csv"
)
# Ministero della Salute — Farmacie attive (aggiornamento settimanale)
# NOTA: l'URL contiene la data di estrazione (YYYYMMDD), da aggiornare periodicamente.
# Per trovare il link corrente: https://www.dati.salute.gov.it/it/dataset/farmacie
# Il server usa TLS 1.0 legacy — richiede ciphers @SECLEVEL=1.
SALUTE_FARMACIE_URL = (
    "https://www.dati.salute.gov.it/sites/default/files/opendata/FRM_FARMA_5_20260321.csv"
)

TIMEOUT_SECONDI = 90
VERSIONE_DATI   = datetime.today().strftime("%Y.%m")


# ── Download helpers ──────────────────────────────────────────────────────────

@retry(
    stop=stop_after_attempt(5),
    wait=wait_exponential(multiplier=1, min=2, max=30),
    retry=retry_if_exception_type((requests.exceptions.RequestException, IOError)),
    before_sleep=before_sleep_log(log, logging.WARNING),
    reraise=True,
)
def scarica_bytes(url: str) -> bytes:
    log.info(f"Download: {url}")
    r = requests.get(url, timeout=TIMEOUT_SECONDI, stream=True)
    r.raise_for_status()
    totale = int(r.headers.get("content-length", 0))
    buf = b""
    with tqdm(total=totale, unit="B", unit_scale=True, desc=Path(url).name) as pb:
        for chunk in r.iter_content(chunk_size=8192):
            buf += chunk
            pb.update(len(chunk))
    return buf


def _detect_enc(raw: bytes, fallback: str = "utf-8") -> str:
    rilevato = chardet.detect(raw[:4096])
    return rilevato.get("encoding") or fallback


def tenta_scarica_csv(url: str, cache_file: Path, offline: bool,
                      sep: str = ";", encoding: str | None = None) -> pd.DataFrame | None:
    if offline:
        if cache_file.exists():
            log.info(f"Offline: {cache_file}")
            enc = encoding or _detect_enc(cache_file.read_bytes())
            return pd.read_csv(str(cache_file), sep=sep, dtype=str,
                               encoding=enc, on_bad_lines="skip")
        log.warning(f"Offline ma cache mancante: {cache_file}")
        return None
    try:
        raw = scarica_bytes(url)
        cache_file.write_bytes(raw)
        enc = encoding or _detect_enc(raw)
        log.info(f"Encoding: {enc}")
        return pd.read_csv(io.BytesIO(raw), sep=sep, dtype=str, encoding=enc,
                           on_bad_lines="skip")
    except Exception as e:
        log.error(f"Download fallito ({url}): {e}")
        if cache_file.exists():
            log.warning(f"Fallback cache: {cache_file}")
            return pd.read_csv(str(cache_file), sep=sep, dtype=str,
                               encoding=encoding or "utf-8", on_bad_lines="skip")
        return None


def tenta_scarica_zip_tsv(url: str, cache_file: Path, offline: bool,
                           pattern: str = "") -> pd.DataFrame | None:
    """ZIP contenente un TSV senza header (es. GeoNames)."""
    if offline:
        if cache_file.exists():
            log.info(f"Offline: {cache_file}")
            return pd.read_csv(str(cache_file), sep="\t", dtype=str,
                               encoding="utf-8", header=None, on_bad_lines="skip")
        return None
    try:
        raw = scarica_bytes(url)
        with zipfile.ZipFile(io.BytesIO(raw)) as z:
            names = [n for n in z.namelist() if pattern.lower() in n.lower()] or [z.namelist()[0]]
            log.info(f"File nel ZIP: {z.namelist()} → uso {names[0]}")
            with z.open(names[0]) as f:
                content = f.read()
        cache_file.write_bytes(content)
        return pd.read_csv(io.BytesIO(content), sep="\t", dtype=str,
                           encoding="utf-8", header=None, on_bad_lines="skip")
    except Exception as e:
        log.error(f"Download ZIP TSV fallito ({url}): {e}")
        if cache_file.exists():
            return pd.read_csv(str(cache_file), sep="\t", dtype=str,
                               encoding="utf-8", header=None, on_bad_lines="skip")
        return None


def tenta_scarica_zip_csv_interno(url: str, cache_file: Path, offline: bool,
                                   sep: str = ";", pattern: str = ".csv") -> pd.DataFrame | None:
    """ZIP contenente un CSV con header (es. ISTAT variazioni, GLEIF)."""
    if offline:
        if cache_file.exists():
            log.info(f"Offline: {cache_file}")
            enc = _detect_enc(cache_file.read_bytes())
            return pd.read_csv(str(cache_file), sep=sep, dtype=str,
                               encoding=enc, on_bad_lines="skip")
        return None
    try:
        raw = scarica_bytes(url)
        with zipfile.ZipFile(io.BytesIO(raw)) as z:
            names = [n for n in z.namelist() if pattern.lower() in n.lower()] or [z.namelist()[0]]
            log.info(f"File nel ZIP: {z.namelist()} → uso {names[0]}")
            with z.open(names[0]) as f:
                content = f.read()
        cache_file.write_bytes(content)
        enc = _detect_enc(content)
        return pd.read_csv(io.BytesIO(content), sep=sep, dtype=str,
                           encoding=enc, on_bad_lines="skip")
    except Exception as e:
        log.error(f"Download ZIP CSV fallito ({url}): {e}")
        if cache_file.exists():
            enc = _detect_enc(cache_file.read_bytes())
            return pd.read_csv(str(cache_file), sep=sep, dtype=str,
                               encoding=enc, on_bad_lines="skip")
        return None


def tenta_scarica_xlsx(url: str, cache_file: Path, offline: bool,
                        sheet: int = 0) -> pd.DataFrame | None:
    """Scarica un file XLSX e lo restituisce come DataFrame."""
    if offline:
        if cache_file.exists():
            log.info(f"Offline: {cache_file}")
            return pd.read_excel(str(cache_file), sheet_name=sheet, dtype=str)
        return None
    try:
        raw = scarica_bytes(url)
        cache_file.write_bytes(raw)
        return pd.read_excel(io.BytesIO(raw), sheet_name=sheet, dtype=str)
    except Exception as e:
        log.error(f"Download XLSX fallito ({url}): {e}")
        if cache_file.exists():
            return pd.read_excel(str(cache_file), sheet_name=sheet, dtype=str)
        return None


def tenta_scarica_zip_csv(url: str, cache_file: Path, offline: bool,
                           pattern: str = "", sep: str = "|") -> pd.DataFrame | None:
    """ZIP contenente un CSV con header (es. IndicePA)."""
    if offline:
        if cache_file.exists():
            log.info(f"Offline: {cache_file}")
            return pd.read_csv(str(cache_file), sep=sep, dtype=str,
                               encoding="utf-8", on_bad_lines="skip")
        return None
    try:
        raw = scarica_bytes(url)
        with zipfile.ZipFile(io.BytesIO(raw)) as z:
            names = [n for n in z.namelist() if pattern.lower() in n.lower()] or [z.namelist()[0]]
            log.info(f"File nel ZIP IPA: {z.namelist()} → uso {names[0]}")
            with z.open(names[0]) as f:
                content = f.read()
        cache_file.write_bytes(content)
        enc = _detect_enc(content)
        return pd.read_csv(io.BytesIO(content), sep=sep, dtype=str,
                           encoding=enc, on_bad_lines="skip")
    except Exception as e:
        log.error(f"Download ZIP CSV fallito ({url}): {e}")
        if cache_file.exists():
            return pd.read_csv(str(cache_file), sep=sep, dtype=str,
                               encoding="utf-8", on_bad_lines="skip")
        return None


# ── Schema SQL ────────────────────────────────────────────────────────────────

SCHEMA_SQL = """
PRAGMA journal_mode=WAL;
PRAGMA foreign_keys=ON;

CREATE TABLE IF NOT EXISTS comuni (
    codice_belfiore     TEXT PRIMARY KEY,
    codice_istat        TEXT NOT NULL UNIQUE,
    denominazione       TEXT NOT NULL,
    denominazione_alt   TEXT,
    sigla_provincia     TEXT NOT NULL,
    nome_provincia      TEXT NOT NULL,
    codice_provincia    TEXT NOT NULL,
    nome_regione        TEXT NOT NULL,
    codice_regione      TEXT NOT NULL,
    ripartizione        INTEGER NOT NULL,
    is_capoluogo        INTEGER NOT NULL DEFAULT 0,
    is_citta_metro      INTEGER NOT NULL DEFAULT 0,
    is_montano          INTEGER NOT NULL DEFAULT 0,
    is_attivo           INTEGER NOT NULL DEFAULT 1,
    data_istituzione    TEXT,
    data_soppressione   TEXT,
    codice_successore   TEXT,
    cap_principale      TEXT,
    latitudine          REAL,
    longitudine         REAL,
    altitudine          REAL,
    superficie_kmq      REAL,
    popolazione         INTEGER,
    anno_rilevazione    INTEGER,
    zona_sismica        INTEGER,
    zona_climatica      TEXT,
    zona_altimetrica    INTEGER,
    classe_aree_interne TEXT,
    nuts3               TEXT,
    nuts2               TEXT,
    nuts1               TEXT,
    santo_patrono       TEXT,
    patrono_giorno      INTEGER,
    patrono_mese        INTEGER,
    pec                 TEXT,
    -- Gestione rifiuti — ISPRA Catasto Rifiuti (aggiornamento annuale)
    perc_raccolta_diff  REAL,      -- % raccolta differenziata (es. 62.61)
    rifiuti_kg_ab       REAL,      -- totale rifiuti urbani kg/abitante
    rifiuti_tot_t       REAL,      -- totale RU prodotti (tonnellate)
    rifiuti_ind_t       REAL,      -- indifferenziato (tonnellate)
    rifiuti_rd_t        REAL,      -- totale RD (tonnellate)
    rd_umido_t          REAL,      -- frazione umida (t)
    rd_carta_t          REAL,      -- carta e cartone (t)
    rd_vetro_t          REAL,      -- vetro (t)
    rd_plastica_t       REAL,      -- plastica (t)
    rd_legno_t          REAL,      -- legno (t)
    rd_metallo_t        REAL,      -- metallo (t)
    rd_verde_t          REAL,      -- verde (t)
    rd_raee_t           REAL,      -- RAEE (t)
    anno_rifiuti        INTEGER    -- anno di riferimento del dato ISPRA
);

CREATE TABLE IF NOT EXISTS variazioni_storiche (
    id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    codice_belfiore     TEXT NOT NULL,
    tipo_variazione     TEXT NOT NULL,
    data_variazione     TEXT NOT NULL,
    denominazione_prec  TEXT,
    denominazione_succ  TEXT,
    provincia_prec      TEXT,
    provincia_succ      TEXT,
    codici_origine      TEXT,
    codici_destinazione TEXT,
    riferimento_norm    TEXT,
    note                TEXT
);

CREATE TABLE IF NOT EXISTS cap (
    id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    cap                 TEXT NOT NULL,
    codice_belfiore     TEXT NOT NULL,
    codice_istat        TEXT NOT NULL,
    descrizione_zona    TEXT,
    data_attivazione    TEXT,
    data_disattivazione TEXT
);

CREATE TABLE IF NOT EXISTS prefissi_telefonici (
    prefisso            TEXT PRIMARY KEY,
    tipo                TEXT NOT NULL,
    area_geografica     TEXT,
    codici_istat        TEXT,
    is_attivo           INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS operatori_mobili (
    prefisso            TEXT PRIMARY KEY,
    nome_operatore      TEXT NOT NULL,
    tecnologia          TEXT NOT NULL,
    is_attivo           INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS dati_demografici (
    codice_belfiore     TEXT NOT NULL,
    anno                INTEGER NOT NULL,
    popolazione         INTEGER,
    maschi              INTEGER,
    femmine             INTEGER,
    superficie_kmq      REAL,
    PRIMARY KEY (codice_belfiore, anno)
);

CREATE TABLE IF NOT EXISTS banche (
    codice_abi          TEXT PRIMARY KEY,
    nome_banca          TEXT NOT NULL,
    codice_bic          TEXT,
    sede_legale         TEXT,
    cap_sede            TEXT,
    comune_sede         TEXT,
    provincia_sede      TEXT,
    is_attivo           INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS ateco (
    codice              TEXT PRIMARY KEY,
    descrizione         TEXT NOT NULL,
    livello             TEXT NOT NULL,
    codice_padre        TEXT,
    note                TEXT
);

CREATE TABLE IF NOT EXISTS ipa_enti (
    codice_ipa          TEXT PRIMARY KEY,
    nome_ente           TEXT NOT NULL,
    codice_fiscale      TEXT,
    codice_sdi          TEXT,
    codice_istat_comune TEXT,
    codice_belfiore     TEXT,
    tipo_ente           TEXT,
    sito_web            TEXT,
    pec                 TEXT,
    data_aggiornamento  TEXT
);

CREATE TABLE IF NOT EXISTS farmacie (
    cod_farmacia      INTEGER PRIMARY KEY,
    denominazione     TEXT,
    indirizzo         TEXT,
    cap               TEXT,
    comune            TEXT,
    frazione          TEXT,
    sigla_provincia   TEXT,
    provincia         TEXT,
    regione           TEXT,
    cod_comune_istat  TEXT,   -- 6 cifre, join su comuni.codice_istat
    tipologia         TEXT,
    latitudine        REAL,
    longitudine       REAL
);

CREATE TABLE IF NOT EXISTS impianti_carburante (
    id_impianto         INTEGER PRIMARY KEY,
    gestore             TEXT,
    bandiera            TEXT,
    tipo_impianto       TEXT,
    nome_impianto       TEXT,
    indirizzo           TEXT,
    comune              TEXT,
    sigla_provincia     TEXT,
    latitudine          REAL,
    longitudine         REAL
);

CREATE TABLE IF NOT EXISTS meta (
    chiave              TEXT PRIMARY KEY,
    valore              TEXT NOT NULL
);

-- Indici comuni
CREATE INDEX IF NOT EXISTS idx_comuni_denominazione ON comuni(denominazione);
CREATE INDEX IF NOT EXISTS idx_comuni_provincia     ON comuni(sigla_provincia);
CREATE INDEX IF NOT EXISTS idx_comuni_regione       ON comuni(nome_regione);
CREATE INDEX IF NOT EXISTS idx_comuni_attivo        ON comuni(is_attivo);
-- Indici CAP
CREATE INDEX IF NOT EXISTS idx_cap_cap              ON cap(cap);
CREATE INDEX IF NOT EXISTS idx_cap_belfiore         ON cap(codice_belfiore);
-- Indici variazioni
CREATE INDEX IF NOT EXISTS idx_variazioni_belfiore  ON variazioni_storiche(codice_belfiore);
-- Indici banche
CREATE INDEX IF NOT EXISTS idx_banche_bic           ON banche(codice_bic);
-- Indici ATECO
CREATE INDEX IF NOT EXISTS idx_ateco_padre          ON ateco(codice_padre);
-- Indici IPA
CREATE INDEX IF NOT EXISTS idx_ipa_belfiore         ON ipa_enti(codice_belfiore);
CREATE INDEX IF NOT EXISTS idx_ipa_sdi              ON ipa_enti(codice_sdi);
CREATE INDEX IF NOT EXISTS idx_ipa_cf               ON ipa_enti(codice_fiscale);
-- Indici farmacie
CREATE INDEX IF NOT EXISTS idx_farm_provincia       ON farmacie(sigla_provincia);
CREATE INDEX IF NOT EXISTS idx_farm_comune          ON farmacie(comune);
CREATE INDEX IF NOT EXISTS idx_farm_istat           ON farmacie(cod_comune_istat);
-- Indici carburanti
CREATE INDEX IF NOT EXISTS idx_carb_provincia       ON impianti_carburante(sigla_provincia);
CREATE INDEX IF NOT EXISTS idx_carb_comune          ON impianti_carburante(comune);

CREATE TABLE IF NOT EXISTS aggregazioni_sovracomunali (
    codice_belfiore     TEXT PRIMARY KEY,
    -- Sanitario
    codice_asl          TEXT,
    nome_asl            TEXT,
    -- Comunità Montana
    comunita_montana    TEXT,
    is_montano          INTEGER NOT NULL DEFAULT 0,
    -- Unione Comuni
    unione_comuni       TEXT,
    -- Ambiti Territoriali Ottimali
    ato_acqua           TEXT,
    ato_rifiuti         TEXT,
    -- Distretto Scolastico
    distretto_scolastico TEXT,
    -- Giustizia
    tribunale           TEXT,
    -- INPS/INAIL
    codice_sede_inps    TEXT,
    codice_sede_inail   TEXT
);

CREATE INDEX IF NOT EXISTS idx_aggregazioni_belfiore ON aggregazioni_sovracomunali(codice_belfiore);

CREATE VIRTUAL TABLE IF NOT EXISTS comuni_fts USING fts5(
    codice_belfiore UNINDEXED,
    denominazione,
    denominazione_alt,
    content='comuni',
    content_rowid='rowid'
);
"""

RIPARTIZIONE_MAP = {"1": 1, "2": 2, "3": 3, "4": 4, "5": 5}


# ── Utility ───────────────────────────────────────────────────────────────────

def pulisci(val) -> str | None:
    if val is None:
        return None
    s = str(val).strip()
    return s if s and s.lower() not in ("nan", "none", "") else None


def to_int(val) -> int | None:
    try:
        return int(float(val)) if val and str(val).strip() not in ("", "nan") else None
    except (ValueError, TypeError):
        return None


def to_float(val) -> float | None:
    try:
        return float(str(val).replace(",", ".")) if val and str(val).strip() not in ("", "nan") else None
    except (ValueError, TypeError):
        return None


# ── 1. Comuni ISTAT ───────────────────────────────────────────────────────────

def carica_comuni(conn: sqlite3.Connection, df: pd.DataFrame) -> int:
    log.info(f"Caricamento {len(df)} comuni ISTAT...")
    righe = []
    for _, r in df.iterrows():
        codice_istat = pulisci(
            r.get("Codice Comune formato alfanumerico") or
            r.get("Codice Comune") or r.get("PRO_COM_T")
        )
        belfiore = pulisci(
            r.get("Codice Catastale del comune") or
            r.get("Codice Belfiore") or r.get("BELFIORE")
        )
        denominazione = pulisci(
            r.get("Denominazione in italiano") or
            r.get("Denominazione (Italiana e straniera)") or
            r.get("Denominazione (Italiano e Inglese)") or r.get("COMUNE")
        )
        sigla_prov = pulisci(r.get("Sigla automobilistica") or r.get("SIGLA"))

        nome_prov = None
        for col in r.index:
            if "Unit" in col and "sovracomunale" in col and "Denominazione" in col:
                nome_prov = pulisci(r.get(col))
                break
        if not nome_prov:
            nome_prov = pulisci(r.get("Denominazione provincia") or r.get("PROVINCIA"))

        cod_prov = pulisci(
            r.get("Codice Provincia (Storico)(1)") or
            r.get("Codice della Provincia (formato numerico)")
        )
        nome_reg  = pulisci(r.get("Denominazione Regione") or r.get("REGIONE"))
        cod_reg   = pulisci(r.get("Codice Regione") or r.get("COD_REG"))
        rip_raw   = pulisci(
            r.get("Codice Ripartizione Geografica") or
            r.get("Ripartizione geografica") or r.get("COD_RIP")
        )
        ripartizione = RIPARTIZIONE_MAP.get(str(rip_raw).strip(), 3) if rip_raw else 3

        if not all([codice_istat, belfiore, denominazione, sigla_prov]):
            continue

        # Zona altimetrica ISTAT: "1"=Pianura, "2"=Collina interna,
        # "3"=Collina litoranea, "4"=Montagna interna, "5"=Montagna litoranea
        zona_alt_raw = pulisci(
            r.get("Zona altimetrica") or
            r.get("Zona Altimetrica") or
            r.get("zona_altimetrica")
        )
        zona_alt = to_int(zona_alt_raw) if zona_alt_raw else None

        # Superficie territoriale — cerca colonna per substring
        col_sup = next((c for c in r.index if "Superficie" in c and "Km" in c), None)
        if not col_sup:
            col_sup = next((c for c in r.index if "superficie" in c.lower()), None)
        superficie = to_float(r.get(col_sup)) if col_sup else None

        # Altitudine del centro — cerca colonna per substring
        col_alt = next((c for c in r.index if "Altitudine" in c and "centro" in c.lower()), None)
        if not col_alt:
            col_alt = next((c for c in r.index if "altitudine" in c.lower()), None)
        altitudine = to_float(r.get(col_alt)) if col_alt else None

        # Codici NUTS — colonne reali: "Codice NUTS1 2021", "Codice NUTS2 2021 (3) ", "Codice NUTS3 2021"
        # Preferiamo 2024 se presente, altrimenti 2021
        nuts1 = pulisci(next((r.get(c) for c in r.index if "NUTS1" in c and "2024" in c), None)) or \
                pulisci(next((r.get(c) for c in r.index if "NUTS1" in c), None))
        nuts2 = pulisci(next((r.get(c) for c in r.index if "NUTS2" in c and "2024" in c), None)) or \
                pulisci(next((r.get(c) for c in r.index if "NUTS2" in c), None))
        nuts3 = pulisci(next((r.get(c) for c in r.index if "NUTS3" in c and "2024" in c), None)) or \
                pulisci(next((r.get(c) for c in r.index if "NUTS3" in c), None))

        righe.append((
            belfiore, codice_istat, denominazione,
            pulisci(r.get("Denominazione in altra lingua")),
            sigla_prov, nome_prov or "", cod_prov or "",
            nome_reg or "", cod_reg or "", ripartizione,
            1 if str(pulisci(next((r.get(c) for c in r.index if "capoluogo" in c.lower() or "Flag" in c), "")) or "").upper() in ("SI", "S", "1", "TRUE") else 0,
            1 if str(pulisci(next((r.get(c) for c in r.index if "metropolitana" in c.lower() or "metro" in c.lower()), "")) or "").upper() in ("SI", "S", "1", "TRUE") else 0,
            0, 1,
            None, None, None, None,   # date, successore, cap_principale
            None, None, altitudine, superficie,   # lat, lng, alt, sup
            None, None,               # pop, anno
            None, None, zona_alt, None,  # zona_sismica, zona_climatica, zona_altimetrica, classe_aree_interne
            nuts3, nuts2, nuts1,      # NUTS
        ))

    conn.executemany("""
        INSERT OR REPLACE INTO comuni (
            codice_belfiore, codice_istat, denominazione, denominazione_alt,
            sigla_provincia, nome_provincia, codice_provincia,
            nome_regione, codice_regione, ripartizione,
            is_capoluogo, is_citta_metro, is_montano, is_attivo,
            data_istituzione, data_soppressione, codice_successore, cap_principale,
            latitudine, longitudine, altitudine, superficie_kmq,
            popolazione, anno_rilevazione,
            zona_sismica, zona_climatica, zona_altimetrica, classe_aree_interne,
            nuts3, nuts2, nuts1
        ) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
    """, righe)
    conn.commit()
    log.info(f"Inseriti {len(righe)} comuni.")
    return len(righe)


# ── 2. Variazioni Storiche ────────────────────────────────────────────────────
# Colonne reali CSV ISTAT (ZIP dal-1991):
#   Anno, Tipo variazione (CD/SF/IS/CS/...), Codice Regione,
#   Codice Unità territoriale sovracomunale, Codice Comune formato alfanumerico,
#   Denominazione Comune, Codice Regione associato alla variazione,
#   Codice Unità territoriale sovracomunale associato alla variazione,
#   Codice del Comune associato alla variazione o nuovo codice Istat del Comune,
#   Denominazione Comune associata alla variazione o nuova denominazione,
#   Provvedimento e Documento, Contenuto del provvedimento,
#   Data decorrenza validità amministrativa, Flag_note

def carica_variazioni_storiche(conn: sqlite3.Connection, df: pd.DataFrame) -> int:
    log.info(f"Caricamento {len(df)} variazioni storiche...")
    log.info(f"Colonne: {list(df.columns)}")

    # Mappa codici ISTAT tipo variazione → enum interno
    tipo_map = {
        "CD": "CambioDenominazione",
        "SF": "Fusione",
        "IS": "Istituzione",
        "CS": "CambioProvincia",
        "AN": "Annessione",
        "SC": "Scissione",
        "SP": "Soppressione",
        # fallback numerico (vecchio formato)
        "1": "Istituzione", "2": "Soppressione", "3": "Fusione",
        "4": "Scissione", "5": "CambioDenominazione", "6": "CambioProvincia",
    }

    # Indice codice_istat → codice_belfiore per il join
    cur = conn.execute("SELECT codice_istat, codice_belfiore FROM comuni")
    istat_to_belfiore = {row[0]: row[1] for row in cur.fetchall()}

    # Colonne del CSV (nome completo con possibili caratteri speciali da encoding)
    col_tipo      = next((c for c in df.columns if "Tipo" in c and "variazione" in c.lower()), None)
    col_istat     = next((c for c in df.columns if "Codice Comune formato" in c), None)
    col_den       = next((c for c in df.columns if c.strip() == "Denominazione Comune"), None)
    col_istat_succ = next((c for c in df.columns if "associato alla variazione" in c and "Codice del Comune" in c), None)
    col_den_succ  = next((c for c in df.columns if "associata alla variazione" in c), None)
    col_data      = next((c for c in df.columns if "Data decorrenza" in c), None)
    col_rif       = next((c for c in df.columns if "Provvedimento" in c), None)
    col_anno      = next((c for c in df.columns if c.strip() == "Anno"), None)

    log.info(f"  tipo={col_tipo} istat={col_istat} data={col_data} succ={col_istat_succ}")

    if not col_tipo or not col_istat:
        log.error("Colonne variazioni storiche non trovate — skip.")
        return 0

    righe = []
    for _, r in df.iterrows():
        istat    = pulisci(r.get(col_istat))
        tipo_raw = pulisci(r.get(col_tipo, ""))
        data_raw = pulisci(r.get(col_data)) if col_data else None
        anno     = pulisci(r.get(col_anno)) if col_anno else None

        if not istat or not tipo_raw:
            continue

        # Converti data: se mancante usa anno
        if not data_raw and anno:
            data_raw = f"01/01/{anno}"
        if not data_raw:
            continue

        tipo       = tipo_map.get(str(tipo_raw).strip().upper(), "CambioDenominazione")
        belfiore   = istat_to_belfiore.get(istat.zfill(6), "")
        istat_succ = pulisci(r.get(col_istat_succ)) if col_istat_succ else None
        belfiore_succ = istat_to_belfiore.get((istat_succ or "").zfill(6), "") if istat_succ else None
        den        = pulisci(r.get(col_den)) if col_den else None
        den_succ   = pulisci(r.get(col_den_succ)) if col_den_succ else None
        rif        = pulisci(r.get(col_rif)) if col_rif else None

        righe.append((
            belfiore or istat,
            tipo,
            data_raw,
            den,           # denominazione_prec
            den_succ,      # denominazione_succ
            None,          # provincia_prec
            None,          # provincia_succ
            json.dumps([istat]),
            json.dumps([istat_succ or istat]),
            rif,
            None,
        ))

        if tipo == "Soppressione" and belfiore:
            conn.execute(
                "UPDATE comuni SET is_attivo=0, data_soppressione=?, codice_successore=? "
                "WHERE codice_belfiore=?",
                (data_raw, belfiore_succ, belfiore)
            )

    conn.executemany("""
        INSERT INTO variazioni_storiche (
            codice_belfiore, tipo_variazione, data_variazione,
            denominazione_prec, denominazione_succ,
            provincia_prec, provincia_succ,
            codici_origine, codici_destinazione,
            riferimento_norm, note
        ) VALUES (?,?,?,?,?,?,?,?,?,?,?)
    """, righe)
    conn.commit()
    log.info(f"Inserite {len(righe)} variazioni storiche.")
    return len(righe)


# ── 3. GeoNames: CAP + coordinate ────────────────────────────────────────────
# Colonne GeoNames IT.txt (tab-sep, no header):
# 0=country 1=postal_code 2=place_name 3=admin_name1(regione) 4=admin_code1
# 5=admin_name2(provincia) 6=admin_code2(sigla) 7=admin_name3 8=admin_code3
# 9=latitude 10=longitude 11=accuracy

def carica_geonames(conn: sqlite3.Connection, df: pd.DataFrame) -> int:
    log.info(f"Caricamento {len(df)} record GeoNames...")

    cur = conn.execute("SELECT denominazione, sigla_provincia, codice_belfiore, codice_istat FROM comuni WHERE is_attivo=1")
    comuni_idx: dict[tuple, tuple] = {}
    for den, prov, belf, istat in cur.fetchall():
        comuni_idx[(den.upper(), prov.upper())] = (belf, istat)

    righe_cap: list[tuple] = []
    aggiornamenti_coord: list[tuple] = []
    visti: set[tuple] = set()

    for _, r in df.iterrows():
        cap      = pulisci(r.iloc[1])
        nome     = pulisci(r.iloc[2])
        prov_raw = pulisci(r.iloc[6])   # admin_code2
        lat      = to_float(r.iloc[9])
        lng      = to_float(r.iloc[10])

        if not cap or not nome:
            continue

        # GeoNames usa "IT-MI" come admin_code2
        if prov_raw and prov_raw.startswith("IT-"):
            prov_raw = prov_raw[3:]

        key   = (nome.upper(), (prov_raw or "").upper())
        match = comuni_idx.get(key)
        if not match:
            for (k_nome, _), v in comuni_idx.items():
                if k_nome == nome.upper():
                    match = v
                    break

        belfiore = match[0] if match else ""
        istat    = match[1] if match else ""

        cap_key = (cap, belfiore)
        if cap_key not in visti:
            visti.add(cap_key)
            righe_cap.append((cap, belfiore, istat, nome, None, None))

        if belfiore and lat is not None and lng is not None:
            aggiornamenti_coord.append((lat, lng, belfiore))

    conn.executemany(
        "INSERT OR IGNORE INTO cap (cap, codice_belfiore, codice_istat, descrizione_zona) VALUES (?,?,?,?)",
        [(r[0], r[1], r[2], r[3]) for r in righe_cap],
    )

    for lat, lng, belfiore in aggiornamenti_coord:
        conn.execute(
            "UPDATE comuni SET latitudine=?, longitudine=? "
            "WHERE codice_belfiore=? AND latitudine IS NULL",
            (lat, lng, belfiore),
        )

    conn.execute("""
        UPDATE comuni SET cap_principale = (
            SELECT cap FROM cap
            WHERE cap.codice_belfiore = comuni.codice_belfiore
            ORDER BY cap LIMIT 1
        ) WHERE cap_principale IS NULL
    """)
    conn.commit()
    log.info(f"Inseriti {len(righe_cap)} CAP, aggiornate coordinate per {len(aggiornamenti_coord)} record.")
    return len(righe_cap)


# ── 4. Zone Climatiche ────────────────────────────────────────────────────────

def carica_zone_climatiche(conn: sqlite3.Connection, df: pd.DataFrame) -> int:
    """
    Carica zone climatiche DPR 412/93 da CSV ENEA Solaritaly.
    Formato: cod reg, cod prov, cod com, sigla prov, comune, altit, gradi-giorni, zona clim
    Il codice ISTAT a 6 cifre si ricava da cod_prov(3) + cod_com(3).
    """
    log.info(f"Caricamento zone climatiche ({len(df)} record)...")

    # Colonne ENEA: "cod prov", "cod com", "zona clim"
    col_prov = next((c for c in df.columns if "prov" in c.lower() and "cod" in c.lower()), None)
    col_com  = next((c for c in df.columns if "com" in c.lower() and "cod" in c.lower() and "prov" not in c.lower()), None)
    col_zona = next((c for c in df.columns if "zona" in c.lower() or "clim" in c.lower()), None)

    if not col_prov or not col_com or not col_zona:
        log.warning(f"Zone climatiche: colonne non trovate in {list(df.columns)} — skip")
        return 0

    aggiornati = 0
    for _, r in df.iterrows():
        prov = pulisci(str(r.get(col_prov, "")))
        com  = pulisci(str(r.get(col_com, "")))
        zona = pulisci(str(r.get(col_zona, "")))
        if not prov or not com or not zona:
            continue
        istat6 = prov.zfill(3) + com.zfill(3)
        res = conn.execute(
            "UPDATE comuni SET zona_climatica=? WHERE codice_istat=?",
            (zona.upper(), istat6),
        )
        aggiornati += res.rowcount

    conn.commit()
    log.info(f"Zone climatiche: {aggiornati} comuni aggiornati.")
    return aggiornati


# ── 5. Zone Sismiche ──────────────────────────────────────────────────────────

def carica_zone_sismiche(conn: sqlite3.Connection, df: pd.DataFrame) -> int:
    log.info(f"Caricamento zone sismiche ({len(df)} record)...")
    log.info(f"Colonne: {list(df.columns)}")

    # Colonne reali: COD_ISTAT_COMUNE, ZONA_SISMICA (es. "3", "2A", "2B")
    col_istat = next((c for c in df.columns if "COD_ISTAT" in c.upper() or
                      ("ISTAT" in c.upper() and "COMUNE" in c.upper())), None)
    col_zona  = next((c for c in df.columns if "ZONA" in c.upper() and "SISM" in c.upper()), None)

    if not col_istat or not col_zona:
        # fallback posizionale
        col_istat = df.columns[4] if len(df.columns) > 4 else df.columns[0]
        col_zona  = df.columns[5] if len(df.columns) > 5 else df.columns[-1]

    log.info(f"  istat={col_istat} zona={col_zona}")

    aggiornati = 0
    for _, r in df.iterrows():
        istat = pulisci(r.get(col_istat))
        zona  = pulisci(r.get(col_zona))
        if not istat or not zona:
            continue
        # La zona può essere "2A", "2B", "3S" — estrai il numero intero principale
        zona_int = to_int(zona[0]) if zona and zona[0].isdigit() else to_int(zona)
        if zona_int is not None:
            res = conn.execute(
                "UPDATE comuni SET zona_sismica=? WHERE codice_istat=? AND zona_sismica IS NULL",
                (zona_int, istat.zfill(6)),
            )
            aggiornati += res.rowcount

    conn.commit()
    log.info(f"Zone sismiche: {aggiornati} comuni aggiornati.")
    return aggiornati


# ── 6. Banche ABI/BIC ─────────────────────────────────────────────────────────

def carica_banche(conn: sqlite3.Connection, df: pd.DataFrame) -> int:
    log.info(f"Caricamento {len(df)} banche...")
    log.info(f"Colonne: {list(df.columns)}")

    righe = []
    for _, r in df.iterrows():
        abi  = pulisci(r.get("ABI") or r.get("Codice ABI") or r.get("codice_abi") or
                       r.get("CAB") or (r.iloc[0] if len(df.columns) > 0 else None))
        nome = pulisci(r.get("Denominazione") or r.get("Banca") or r.get("Nome") or
                       r.get("DENOMINAZIONE") or (r.iloc[1] if len(df.columns) > 1 else None))
        bic  = pulisci(r.get("BIC") or r.get("Codice BIC") or r.get("SWIFT") or
                       (r.iloc[2] if len(df.columns) > 2 else None))
        sede = pulisci(r.get("Comune") or r.get("Sede") or r.get("Città"))
        prov = pulisci(r.get("Provincia") or r.get("PROV"))

        if not abi or not nome:
            continue
        righe.append((abi, nome, bic, None, None, sede, prov, 1))

    if righe:
        conn.executemany("""
            INSERT OR REPLACE INTO banche
            (codice_abi, nome_banca, codice_bic, sede_legale, cap_sede,
             comune_sede, provincia_sede, is_attivo)
            VALUES (?,?,?,?,?,?,?,?)
        """, righe)
        conn.commit()
    log.info(f"Inserite {len(righe)} banche.")
    return len(righe)


# ── 6b. Banche da GLEIF BIC-LEI ──────────────────────────────────────────────
# Colonne GLEIF: BIC, LEI, ...
# Filtriamo BIC italiani (iniziano con IT o il paese è derivabile)

def carica_banche_gleif(conn: sqlite3.Connection, df: pd.DataFrame) -> int:
    log.info(f"Caricamento banche GLEIF ({len(df)} record)...")
    log.info(f"Colonne: {list(df.columns[:8])}")

    righe = []
    for _, r in df.iterrows():
        bic = pulisci(r.get("BIC") or r.get("bic") or (r.iloc[0] if len(df.columns) > 0 else None))
        lei = pulisci(r.get("LEI") or r.get("lei") or (r.iloc[1] if len(df.columns) > 1 else None))

        if not bic:
            continue
        # Filtra solo banche italiane (BIC country code = IT, posizioni 4-5)
        if len(bic) >= 6 and bic[4:6].upper() != "IT":
            continue

        # ABI non presente nel GLEIF — usiamo BIC come chiave primaria
        # Prefisso ABI convenzionale: prime 5 lettere del BIC (istituzione)
        abi_proxy = bic[:8]  # BIC11 o BIC8

        righe.append((abi_proxy, bic[:8], bic, None, None, None, None, 1))

    if righe:
        conn.executemany("""
            INSERT OR REPLACE INTO banche
            (codice_abi, nome_banca, codice_bic, sede_legale, cap_sede,
             comune_sede, provincia_sede, is_attivo)
            VALUES (?,?,?,?,?,?,?,?)
        """, righe)
        conn.commit()
    log.info(f"Inserite {len(righe)} banche italiane da GLEIF.")
    return len(righe)


# ── 6c. Banche da Banca d'Italia (ABI ufficiale) ─────────────────────────────

def carica_banche_bancaditalia(conn: sqlite3.Connection,
                                df_banche: pd.DataFrame,
                                df_bic: pd.DataFrame | None = None) -> int:
    """
    Carica il registro ufficiale delle banche da Banca d'Italia.
    df_banche: CSV albo banche (colonne: ABI, Denominazione, Comune, Provincia, ...)
    df_bic:    CSV codici BIC (colonne: ABI, BIC, ...) — opzionale
    """
    log.info(f"Caricamento banche Banca d'Italia ({len(df_banche)} record)...")
    log.info(f"Colonne banche: {list(df_banche.columns)}")

    # Mappa BIC per ABI se disponibile
    bic_map: dict[str, str] = {}
    if df_bic is not None:
        log.info(f"Colonne BIC: {list(df_bic.columns)}")
        for _, r in df_bic.iterrows():
            abi_key = pulisci(r.get("ABI") or r.get("Codice ABI") or r.get("codice_abi") or
                              (r.iloc[0] if len(df_bic.columns) > 0 else None))
            bic_val = pulisci(r.get("BIC") or r.get("Codice BIC") or r.get("codice_bic") or
                              (r.iloc[1] if len(df_bic.columns) > 1 else None))
            if abi_key and bic_val:
                bic_map[abi_key.zfill(5)] = bic_val.upper()

    righe = []
    for _, r in df_banche.iterrows():
        # Prova varie intestazioni possibili del CSV Banca d'Italia
        abi  = pulisci(r.get("ABI") or r.get("Codice ABI") or r.get("COD_ABI") or
                       r.get("codice_abi") or (r.iloc[0] if len(df_banche.columns) > 0 else None))
        nome = pulisci(r.get("Denominazione") or r.get("Banca") or r.get("DENOMINAZIONE") or
                       r.get("Nome") or r.get("RAGIONE_SOCIALE") or
                       (r.iloc[1] if len(df_banche.columns) > 1 else None))
        sede = pulisci(r.get("Comune") or r.get("COMUNE") or r.get("Sede") or r.get("Città"))
        prov = pulisci(r.get("Provincia") or r.get("PROV") or r.get("PROVINCIA"))
        cap  = pulisci(r.get("CAP") or r.get("cap"))
        indirizzo = pulisci(r.get("Indirizzo") or r.get("INDIRIZZO") or r.get("Sede Legale"))

        if not abi or not nome:
            continue

        abi_padded = abi.zfill(5)
        bic = bic_map.get(abi_padded)

        righe.append((abi_padded, nome, bic, indirizzo, cap, sede, prov, 1))

    if righe:
        conn.execute("DELETE FROM banche")
        conn.executemany("""
            INSERT OR REPLACE INTO banche
            (codice_abi, nome_banca, codice_bic, sede_legale, cap_sede,
             comune_sede, provincia_sede, is_attivo)
            VALUES (?,?,?,?,?,?,?,?)
        """, righe)
        conn.commit()
    log.info(f"Inserite {len(righe)} banche da Banca d'Italia.")
    return len(righe)


# ── 7. ATECO 2007 ─────────────────────────────────────────────────────────────

def carica_ateco(conn: sqlite3.Connection, df: pd.DataFrame) -> int:
    log.info(f"Caricamento ATECO ({len(df)} record)...")
    log.info(f"Colonne: {list(df.columns)}")

    # Colonne reali ISTAT: "Codice\nAteco 2007\naggiornamento 2022", "Titolo Ateco 2007 aggiornamento 2022"
    col_codice = next((c for c in df.columns if "Ateco" in c or "ATECO" in c or
                       c.lower() in ("codice", "ateco")), df.columns[0])
    col_desc   = next((c for c in df.columns if "Titolo" in c or "Descrizione" in c or
                       "DESCRIZIONE" in c.upper() or c.lower() == "descrizione"), df.columns[1] if len(df.columns) > 1 else df.columns[0])
    log.info(f"  codice={col_codice!r} desc={col_desc!r}")

    righe = []
    for _, r in df.iterrows():
        codice = pulisci(r.get(col_codice) or (r.iloc[0] if len(df.columns) > 0 else None))
        descrizione = pulisci(r.get(col_desc) or (r.iloc[1] if len(df.columns) > 1 else None))
        if not codice or not descrizione:
            continue

        # Determina livello dal formato: A → Sezione, 10 → Divisione,
        # 10.1 → Gruppo, 10.11 → Classe
        c = codice.strip()
        if len(c) == 1 and c.isalpha():
            livello = "Sezione"
            padre   = None
        elif c.isdigit() and len(c) == 2:
            livello = "Divisione"
            padre   = None
        elif "." in c:
            parti = c.split(".")
            if len(parti[1]) == 1:
                livello = "Gruppo"
                padre   = parti[0]
            else:
                livello = "Classe"
                padre   = c[:-1]
        else:
            livello = "Classe"
            padre   = None

        righe.append((codice, descrizione, livello, padre, None))

    if righe:
        conn.executemany(
            "INSERT OR REPLACE INTO ateco (codice, descrizione, livello, codice_padre, note) "
            "VALUES (?,?,?,?,?)",
            righe,
        )
        conn.commit()
    log.info(f"Inseriti {len(righe)} codici ATECO.")
    return len(righe)


# ── 8. IndicePA ───────────────────────────────────────────────────────────────

def carica_ipa(conn: sqlite3.Connection, df: pd.DataFrame) -> int:
    log.info(f"Caricamento IPA ({len(df)} enti)...")
    log.info(f"Colonne (prime 10): {list(df.columns[:10])}")

    cur = conn.execute("SELECT codice_istat, codice_belfiore FROM comuni")
    istat_to_belfiore = {row[0]: row[1] for row in cur.fetchall()}

    # Colonne reali IPA XLSX:
    # Codice_IPA, Denominazione_ente, Codice_fiscale_ente, Tipologia,
    # Codice_Categoria, Codice_natura, Codice_ateco, Ente_in_liquidazione,
    # Codice_MIUR, Codice_ISTAT, ...
    righe = []
    for _, r in df.iterrows():
        cod_ipa = pulisci(r.get("Codice_IPA") or r.get("cod_amm"))
        nome    = pulisci(r.get("Denominazione_ente") or r.get("des_amm"))
        cf      = pulisci(r.get("Codice_fiscale_ente") or r.get("CF") or r.get("Cod_fiscale"))
        sdi     = pulisci(r.get("Codice_uni_ou") or r.get("Codice_univoco_uo") or r.get("cod_ou"))
        istat_comune = pulisci(r.get("Codice_ISTAT") or r.get("Cod_comune") or r.get("codice_istat"))
        tipo     = pulisci(r.get("Tipologia") or r.get("Codice_Categoria"))
        sito     = pulisci(r.get("Sito_istituzionale") or r.get("sito_web"))
        pec      = pulisci(r.get("Mail1") or r.get("pec") or r.get("PEC"))
        data_agg = pulisci(r.get("Data_accreditamento") or r.get("data_aggiornamento"))

        if not cod_ipa or not nome:
            continue

        if istat_comune and len(istat_comune) < 6:
            istat_comune = istat_comune.zfill(6)
        belfiore = istat_to_belfiore.get(istat_comune or "") if istat_comune else None

        righe.append((cod_ipa, nome, cf, sdi, istat_comune, belfiore, tipo, sito, pec, data_agg))

    if righe:
        conn.executemany("""
            INSERT OR REPLACE INTO ipa_enti
            (codice_ipa, nome_ente, codice_fiscale, codice_sdi,
             codice_istat_comune, codice_belfiore, tipo_ente, sito_web, pec, data_aggiornamento)
            VALUES (?,?,?,?,?,?,?,?,?,?)
        """, righe)
        conn.commit()
    log.info(f"Inseriti {len(righe)} enti IPA.")
    return len(righe)


# ── 9. Aggregazioni Sovracomunali (Comunità Montane ISTAT) ───────────────────
# Fonte: ISTAT — Elenco comuni italiani contiene campo "Comune montano" e
# "Denominazione Unità territoriale sovracomunale" usabile per unioni.
# Le comunità montane storiche ISTAT: file separato non sempre disponibile,
# usiamo il campo is_montano già presente in comuni + nome comunità da CSV ISTAT.

def carica_aggregazioni_sovracomunali(conn: sqlite3.Connection, df_comuni: pd.DataFrame) -> int:
    """
    Popola aggregazioni_sovracomunali con i dati disponibili dal CSV ISTAT comuni.
    Campi estraibili: is_montano, comunita_montana (colonna 'Unità territoriale sovracomunale').
    ASL, INPS, INAIL richiedono fonti separate — lasciati NULL per ora.
    """
    log.info("Caricamento aggregazioni sovracomunali da CSV ISTAT comuni...")

    # Cerca colonna comunità montana nel CSV ISTAT
    col_montana = next(
        (c for c in df_comuni.columns if "montana" in c.lower() or
         ("comunit" in c.lower() and "mont" in c.lower())), None
    )
    col_unione = next(
        (c for c in df_comuni.columns if "unione" in c.lower() and "comun" in c.lower()), None
    )
    col_montano = next(
        (c for c in df_comuni.columns if "montano" in c.lower()), None
    )

    # Indice codice_istat → codice_belfiore
    cur = conn.execute("SELECT codice_istat, codice_belfiore FROM comuni")
    istat_to_belfiore = {row[0]: row[1] for row in cur.fetchall()}

    col_istat = next((c for c in df_comuni.columns if "Codice Comune formato" in c), None)
    col_belfiore = next((c for c in df_comuni.columns if "Catastale" in c or "Belfiore" in c), None)

    righe = []
    for _, r in df_comuni.iterrows():
        belfiore = pulisci(r.get(col_belfiore)) if col_belfiore else None
        if not belfiore:
            istat = pulisci(r.get(col_istat)) if col_istat else None
            belfiore = istat_to_belfiore.get((istat or "").zfill(6)) if istat else None
        if not belfiore:
            continue

        montana = pulisci(r.get(col_montana)) if col_montana else None
        unione  = pulisci(r.get(col_unione))  if col_unione  else None
        montano_raw = pulisci(r.get(col_montano)) if col_montano else None
        is_montano = 1 if montano_raw and str(montano_raw).upper() in ("SI", "S", "1", "TRUE") else 0

        righe.append((belfiore, None, None, montana, is_montano, unione, None, None, None, None, None, None))

    if righe:
        conn.executemany("""
            INSERT OR REPLACE INTO aggregazioni_sovracomunali (
                codice_belfiore, codice_asl, nome_asl,
                comunita_montana, is_montano, unione_comuni,
                ato_acqua, ato_rifiuti, distretto_scolastico,
                tribunale, codice_sede_inps, codice_sede_inail
            ) VALUES (?,?,?,?,?,?,?,?,?,?,?,?)
        """, righe)
        conn.commit()
    log.info(f"Aggregazioni sovracomunali: inseriti {len(righe)} record.")
    return len(righe)


# ── 10. Aree Interne ISTAT ───────────────────────────────────────────────────

def carica_aree_interne(conn: sqlite3.Connection, xlsx_path: Path) -> int:
    """
    Aggiorna comuni.classe_aree_interne dall'XLSX ISTAT aree interne.
    - header alla riga 2 (indice 2, ovvero 3a riga del file)
    - join su codice_belfiore tramite colonna "CODICE CATASTALE"
    - classe da "CODICE_Aree_Interne 2021-2027" (es. "C - Cintura" → "C")
    """
    if not xlsx_path.exists():
        log.warning(f"Aree interne: file non trovato ({xlsx_path}) — skip.")
        return 0

    log.info(f"Caricamento aree interne ISTAT da {xlsx_path}...")
    df = pd.read_excel(str(xlsx_path), header=2, dtype=str)
    log.info(f"Colonne: {list(df.columns)}")

    COL_BELFIORE = "CODICE CATASTALE"
    COL_CLASSE   = "CODICE_Aree_Interne 2021-2027"

    if COL_BELFIORE not in df.columns or COL_CLASSE not in df.columns:
        log.warning(f"Colonne aree interne mancanti — trovate: {list(df.columns)}")
        return 0

    aggiornati = 0
    for _, r in df.iterrows():
        belfiore = pulisci(r.get(COL_BELFIORE))
        classe   = pulisci(r.get(COL_CLASSE))
        if not belfiore or not classe:
            continue
        # Prendi solo il codice lettera (es. "C - Cintura" → "C")
        classe_cod = classe.strip().upper()[0]
        res = conn.execute(
            "UPDATE comuni SET classe_aree_interne=? WHERE codice_belfiore=?",
            (classe_cod, belfiore.upper()),
        )
        aggiornati += res.rowcount

    conn.commit()
    log.info(f"Aree interne: {aggiornati} comuni aggiornati.")
    return aggiornati


# ── 11a. Superficie da comuni_geo ISTAT ───────────────────────────────────────

def carica_comuni_geo(conn: sqlite3.Connection, xlsx_path: Path) -> int:
    """
    Aggiorna comuni.superficie_kmq dall'XLSX ISTAT comuni_geo.
    Sheet "Dati comunali", join su "Codice Comune" (zfill 6 = codice_istat).
    """
    if not xlsx_path.exists():
        log.warning(f"Comuni geo: file non trovato ({xlsx_path}) — skip.")
        return 0

    log.info(f"Caricamento superficie comuni da {xlsx_path}...")
    df = pd.read_excel(str(xlsx_path), sheet_name="Dati comunali", dtype=str)
    log.info(f"Colonne: {list(df.columns)}")

    COL_ISTAT = "Codice Comune"
    COL_SUP   = "Superficie totale (Km2)"

    if COL_ISTAT not in df.columns or COL_SUP not in df.columns:
        log.warning(f"Colonne comuni_geo mancanti — trovate: {list(df.columns)}")
        return 0

    aggiornati = 0
    for _, r in df.iterrows():
        istat = pulisci(r.get(COL_ISTAT))
        sup   = to_float(r.get(COL_SUP))
        if not istat or sup is None:
            continue
        res = conn.execute(
            "UPDATE comuni SET superficie_kmq=? WHERE codice_istat=?",
            (sup, istat.zfill(6)),
        )
        aggiornati += res.rowcount

    conn.commit()
    log.info(f"Superficie comuni: {aggiornati} comuni aggiornati.")
    return aggiornati


# ── 11b. Popolazione ISTAT (per comune, per anno) ─────────────────────────────

def carica_popolazione(conn: sqlite3.Connection, xlsx_path: Path) -> int:
    """
    Aggiorna comuni.popolazione e anno_rilevazione dall'XLSX ISTAT
    "Popolazione per paese di nascita - comuni".
    Struttura: header riga 7 (indice 7), dati da riga 8.
    Colonna 0 = nome comune, colonna 6 = totale 2024, colonna 1 = totale 2021.
    Join su denominazione (case-insensitive, strip).
    """
    if not xlsx_path.exists():
        log.warning(f"Popolazione: file non trovato ({xlsx_path}) — skip.")
        return 0

    log.info(f"Caricamento popolazione da {xlsx_path}...")
    df_raw = pd.read_excel(str(xlsx_path), header=None, dtype=str)

    # Struttura attesa:
    #   Riga 5 (indice 5): Anno — es. NaN, 2021, 2021, ..., 2024, 2024, ...
    #   Riga 6 (indice 6): Paese — es. NaN, Mondo, Paesi esteri, Italia, ..., Mondo, ...
    #   Riga 7 (indice 7): header "Territorio" in col 0
    #   Dati da riga 8 (indice 8): col 0 = nome comune, col 1/6/... = totale per anno
    dati = df_raw.iloc[8:].copy()
    dati.columns = range(len(dati.columns))

    anni_row  = df_raw.iloc[5]
    paese_row = df_raw.iloc[6]

    # Trova la colonna "Mondo" dell'anno più recente
    # Costruisce lista di (anno, col_idx) dove paese == "Mondo"
    coppie = []
    for idx in range(1, len(anni_row)):
        anno_val  = pulisci(str(anni_row[idx]))
        paese_val = pulisci(str(paese_row[idx]))
        if anno_val and anno_val.isdigit() and paese_val and paese_val.lower() == "mondo":
            coppie.append((int(anno_val), idx))

    if coppie:
        anno_pop, col_pop = max(coppie, key=lambda x: x[0])
    else:
        col_pop = 1    # fallback: prima colonna dati
        anno_pop = 2021

    log.info(f"  Popolazione: colonna={col_pop} (Mondo totale), anno={anno_pop}")

    # Costruisci mappa denominazione normalizzata → codice_istat dal DB
    cur = conn.execute("SELECT codice_istat, denominazione FROM comuni WHERE is_attivo=1")
    mappa_nome: dict[str, str] = {}
    for row in cur.fetchall():
        mappa_nome[row[1].strip().upper()] = row[0]

    aggiornati = 0
    for _, r in dati.iterrows():
        nome = pulisci(str(r.get(0, "")))
        if not nome:
            continue
        pop = to_int(r.get(col_pop))
        if pop is None:
            continue
        codice = mappa_nome.get(nome.upper())
        if not codice:
            continue
        conn.execute(
            "UPDATE comuni SET popolazione=?, anno_rilevazione=? WHERE codice_istat=?",
            (pop, anno_pop, codice),
        )
        aggiornati += 1

    conn.commit()
    log.info(f"Popolazione: {aggiornati} comuni aggiornati.")
    return aggiornati


# ── 12. ASL Ministero della Salute ───────────────────────────────────────────

def carica_asl(conn: sqlite3.Connection, xlsx_path: Path) -> int:
    """
    Popola aggregazioni_sovracomunali.codice_asl e nome_asl dall'XLSX
    del Ministero della Salute (formato .xls o .xlsx).
    Colonne: CODICE AZIENDA, DENOMINAZIONE AZIENDA, SIGLA PROVINCIA, ANNO.
    Join su sigla_provincia; si usa il record dell'anno più recente.
    """
    if not xlsx_path.exists():
        log.warning(f"ASL: file non trovato ({xlsx_path}) — skip.")
        return 0

    log.info(f"Caricamento ASL da {xlsx_path}...")
    try:
        df = pd.read_excel(str(xlsx_path), dtype=str)
    except Exception as e:
        log.error(f"Lettura ASL fallita: {e}")
        return 0
    log.info(f"Colonne: {list(df.columns)}")

    COL_COD  = "CODICE AZIENDA"
    COL_NOME = "DENOMINAZIONE AZIENDA"
    COL_PROV = "SIGLA PROVINCIA"
    COL_ANNO = "ANNO"

    if COL_NOME not in df.columns or COL_PROV not in df.columns:
        log.warning(f"Colonne ASL mancanti — trovate: {list(df.columns)}")
        return 0

    # Filtra l'anno più recente disponibile
    if COL_ANNO in df.columns:
        anni = df[COL_ANNO].dropna().unique()
        anno_max = max((a for a in anni if str(a).strip().isdigit()),
                       key=lambda x: int(x), default=None)
        if anno_max:
            df = df[df[COL_ANNO] == anno_max]
            log.info(f"  Filtrato anno {anno_max}: {len(df)} record")

    # Costruisci mappa sigla_provincia → (codice_asl, nome_asl)
    # Una provincia può avere più ASL → list
    asl_per_provincia: dict[str, list[tuple]] = {}
    for _, r in df.iterrows():
        prov     = pulisci(r.get(COL_PROV))
        nome_asl = pulisci(r.get(COL_NOME))
        cod_asl  = pulisci(r.get(COL_COD)) if COL_COD in df.columns else None
        if not prov or not nome_asl:
            continue
        sigla = prov.upper().strip()
        asl_per_provincia.setdefault(sigla, []).append((cod_asl, nome_asl))

    if not asl_per_provincia:
        log.warning("Nessuna ASL caricata — dati non validi.")
        return 0

    # Per ogni comune aggiorna/inserisce in aggregazioni_sovracomunali
    aggiornati = 0
    for sigla, asl_list in asl_per_provincia.items():
        # Se più ASL per la provincia, concatena i nomi
        cod_asl  = asl_list[0][0]
        nome_asl = " / ".join(a[1] for a in asl_list)
        cur = conn.execute(
            "SELECT codice_belfiore FROM comuni WHERE sigla_provincia=? AND is_attivo=1",
            (sigla,),
        )
        belfiori = [row[0] for row in cur.fetchall()]
        for belfiore in belfiori:
            conn.execute("""
                INSERT INTO aggregazioni_sovracomunali
                    (codice_belfiore, codice_asl, nome_asl, is_montano)
                VALUES (?, ?, ?, 0)
                ON CONFLICT(codice_belfiore) DO UPDATE SET
                    codice_asl = excluded.codice_asl,
                    nome_asl   = excluded.nome_asl
            """, (belfiore, cod_asl, nome_asl))
            aggiornati += 1

    conn.commit()
    log.info(f"ASL: {aggiornati} comuni aggiornati ({len(asl_per_provincia)} province mappate).")
    return aggiornati


# ── 13. Prefissi geografici per provincia ────────────────────────────────────

# Fonte: Piano Nazionale di Numerazione AGCOM + verifica incrociata.
# Formato: (sigla_provincia, prefisso_senza_0)
# Nota: i prefissi geografici si compongono aggiungendo "0" davanti (es. SV → 019 → 0019).
# Le province con prefisso a 2 cifre sono: RM (06) e MI (02).
PREFISSI_PROVINCIA: list[tuple[str, str]] = [
    # Nord-Ovest
    ("TO", "11"),   # Torino
    ("VC", "161"),  # Vercelli
    ("NO", "321"),  # Novara
    ("CN", "171"),  # Cuneo
    ("AT", "141"),  # Asti
    ("AL", "131"),  # Alessandria
    ("BI", "15"),   # Biella
    ("VB", "323"),  # Verbano-Cusio-Ossola
    ("AO", "165"),  # Aosta
    ("IM", "183"),  # Imperia
    ("SV", "19"),   # Savona
    ("GE", "10"),   # Genova
    ("SP", "187"),  # La Spezia
    ("VA", "332"),  # Varese
    ("CO", "31"),   # Como
    ("LC", "341"),  # Lecco
    ("SO", "342"),  # Sondrio
    ("MI", "2"),    # Milano  ← prefisso a 1 cifra → "02"
    ("MB", "39"),   # Monza e Brianza
    ("BG", "35"),   # Bergamo
    ("BS", "30"),   # Brescia
    ("PV", "382"),  # Pavia
    ("LO", "371"),  # Lodi
    ("CR", "372"),  # Cremona
    ("MN", "376"),  # Mantova
    # Nord-Est
    ("TN", "461"),  # Trento
    ("BZ", "471"),  # Bolzano
    ("VR", "45"),   # Verona
    ("VI", "444"),  # Vicenza
    ("BL", "437"),  # Belluno
    ("TV", "422"),  # Treviso
    ("VE", "41"),   # Venezia
    ("PD", "49"),   # Padova
    ("RO", "425"),  # Rovigo
    ("UD", "432"),  # Udine
    ("GO", "481"),  # Gorizia
    ("TS", "40"),   # Trieste
    ("PN", "434"),  # Pordenone
    ("PC", "523"),  # Piacenza
    ("PR", "521"),  # Parma
    ("RE", "522"),  # Reggio Emilia
    ("MO", "59"),   # Modena
    ("BO", "51"),   # Bologna
    ("FE", "532"),  # Ferrara
    ("RA", "544"),  # Ravenna
    ("FC", "543"),  # Forlì-Cesena
    ("RN", "541"),  # Rimini
    # Centro
    ("MS", "585"),  # Massa-Carrara
    ("LU", "583"),  # Lucca
    ("PT", "573"),  # Pistoia
    ("FI", "55"),   # Firenze
    ("LI", "586"),  # Livorno
    ("PI", "50"),   # Pisa
    ("AR", "575"),  # Arezzo
    ("SI", "577"),  # Siena
    ("GR", "564"),  # Grosseto
    ("PO", "574"),  # Prato
    ("PG", "75"),   # Perugia
    ("TR", "744"),  # Terni
    ("VT", "761"),  # Viterbo
    ("RI", "746"),  # Rieti
    ("RM", "6"),    # Roma  ← prefisso a 1 cifra → "06"
    ("LT", "773"),  # Latina
    ("FR", "775"),  # Frosinone
    ("AN", "71"),   # Ancona
    ("PU", "721"),  # Pesaro e Urbino
    ("MC", "733"),  # Macerata
    ("AP", "736"),  # Ascoli Piceno
    ("FM", "734"),  # Fermo
    ("TE", "861"),  # Teramo
    ("PE", "85"),   # Pescara
    ("CH", "871"),  # Chieti
    ("AQ", "862"),  # L'Aquila
    ("CB", "874"),  # Campobasso
    ("IS", "865"),  # Isernia
    # Sud
    ("CE", "823"),  # Caserta
    ("BN", "824"),  # Benevento
    # NA (Napoli) non presente nel CSV ISTAT corrente — comuni napoletani non hanno sigla
    ("AV", "825"),  # Avellino
    ("SA", "89"),   # Salerno
    ("FG", "881"),  # Foggia
    ("BA", "80"),   # Bari
    ("BT", "883"),  # Barletta-Andria-Trani
    ("TA", "99"),   # Taranto
    ("BR", "831"),  # Brindisi
    ("LE", "832"),  # Lecce
    ("PZ", "971"),  # Potenza
    ("MT", "835"),  # Matera
    ("CS", "984"),  # Cosenza
    ("CZ", "961"),  # Catanzaro
    ("KR", "962"),  # Crotone
    ("VV", "963"),  # Vibo Valentia
    ("RC", "965"),  # Reggio Calabria
    # Sicilia
    ("TP", "923"),  # Trapani
    ("PA", "91"),   # Palermo
    ("ME", "90"),   # Messina
    ("AG", "922"),  # Agrigento
    ("CL", "934"),  # Caltanissetta
    ("EN", "935"),  # Enna
    ("CT", "95"),   # Catania
    ("RG", "932"),  # Ragusa
    ("SR", "931"),  # Siracusa
    # Sardegna
    ("SS", "79"),   # Sassari
    ("NU", "784"),  # Nuoro
    ("OR", "783"),  # Oristano
    ("CA", "70"),   # Cagliari
    # CI, VS, OG, OT — province sarde soppresse nel 2016, non presenti nel CSV ISTAT corrente
    ("SU", "781"),  # Sud Sardegna (istituita 2016)
]


def carica_prefissi_geografici(conn: sqlite3.Connection) -> int:
    """
    Popola prefissi_telefonici con i prefissi geografici provinciali.
    Per ogni provincia recupera i codici_istat dei comuni e li salva
    come JSON array nella colonna codici_istat.
    """
    log.info("Caricamento prefissi geografici provinciali...")
    inseriti = 0
    for sigla, prefisso in PREFISSI_PROVINCIA:
        cur = conn.execute(
            "SELECT codice_istat FROM comuni WHERE sigla_provincia=? AND is_attivo=1",
            (sigla,),
        )
        codici = [row[0] for row in cur.fetchall()]
        if not codici:
            log.warning(f"  Provincia {sigla}: nessun comune attivo trovato.")
            continue
        # Recupera nome capoluogo per area_geografica
        cap = conn.execute(
            "SELECT denominazione FROM comuni WHERE sigla_provincia=? AND is_capoluogo=1 AND is_attivo=1 LIMIT 1",
            (sigla,),
        ).fetchone()
        area = cap[0] if cap else sigla
        codici_json = json.dumps(codici, ensure_ascii=False)
        conn.execute("""
            INSERT INTO prefissi_telefonici (prefisso, tipo, area_geografica, codici_istat, is_attivo)
            VALUES (?, 'Geografico', ?, ?, 1)
            ON CONFLICT(prefisso) DO UPDATE SET
                area_geografica = excluded.area_geografica,
                codici_istat    = excluded.codici_istat
        """, (prefisso, area, codici_json))
        inseriti += 1

    conn.commit()
    log.info(f"Prefissi geografici: {inseriti} province inserite.")
    return inseriti


# ── Dati default telefonia ────────────────────────────────────────────────────

OPERATORI_DEFAULT = [
    ("320", "WindTre", "LTE"), ("330", "WindTre", "LTE"),
    ("347", "WindTre", "LTE"), ("348", "WindTre", "LTE"),
    ("340", "Vodafone", "LTE"), ("341", "Vodafone", "LTE"),
    ("342", "Vodafone", "LTE"), ("343", "Vodafone", "LTE"),
    ("346", "Vodafone", "LTE"), ("360", "TIM", "LTE"),
    ("366", "TIM", "LTE"), ("368", "TIM", "LTE"), ("370", "TIM", "LTE"),
    ("380", "Iliad", "LTE"), ("351", "PosteMobile", "MVNO"),
    ("328", "KENA Mobile", "MVNO"), ("388", "Very Mobile", "MVNO"),
    ("391", "CoopVoce", "MVNO"), ("392", "Fastweb Mobile", "MVNO"),
]

PREFISSI_DEFAULT = [
    ("02",  "Geografico", "Milano e provincia"),
    ("06",  "Geografico", "Roma"),
    ("081", "Geografico", "Napoli"),
    ("011", "Geografico", "Torino"),
    ("051", "Geografico", "Bologna"),
    ("010", "Geografico", "Genova"),
    ("055", "Geografico", "Firenze"),
    ("091", "Geografico", "Palermo"),
    ("095", "Geografico", "Catania"),
    ("070", "Geografico", "Cagliari"),
    ("040", "Geografico", "Trieste"),
    ("045", "Geografico", "Verona"),
    ("049", "Geografico", "Padova"),
    ("800", "TollFree",   "Numeri verdi"),
    ("803", "TollFree",   "Servizi PA"),
    ("112", "Emergenza",  "Numero Unico Emergenze"),
    ("118", "Emergenza",  "Emergenza Sanitaria"),
    ("113", "Emergenza",  "Polizia di Stato"),
    ("115", "Emergenza",  "Vigili del Fuoco"),
    ("117", "Emergenza",  "Guardia di Finanza"),
]


def carica_pec(conn: sqlite3.Connection, json_path: Path) -> int:
    """
    Popola la colonna pec in comuni dal file tools/pec_comuni.json
    (generato una tantum dal file IndicePA: elenco-amministrazioni-pec-indirizzi.xlsx).
    Una sola PEC per comune (priorità: Uff_eFatturaPA > Ufficio Protocollo > primo).
    """
    if not json_path.exists():
        log.warning(f"pec_comuni.json non trovato in {json_path} — skip caricamento PEC")
        return 0
    import json
    with open(json_path, encoding="utf-8") as f:
        pec_map = json.load(f)
    n = 0
    for belfiore, pec in pec_map.items():
        cur = conn.execute(
            "UPDATE comuni SET pec=? WHERE codice_belfiore=?",
            (pec, belfiore),
        )
        n += cur.rowcount
    conn.commit()
    log.info(f"PEC comuni caricati: {n} record aggiornati")
    return n


def carica_patroni(conn: sqlite3.Connection, json_path: Path) -> int:
    """
    Popola le colonne santo_patrono/patrono_giorno/patrono_mese in comuni
    dal file tools/patroni.json generato da scrape_patroni.py.
    """
    if not json_path.exists():
        log.warning(f"patroni.json non trovato in {json_path} — skip caricamento patroni")
        return 0
    import json
    with open(json_path, encoding="utf-8") as f:
        patroni = json.load(f)
    n = 0
    for belfiore, dati in patroni.items():
        cur = conn.execute(
            "UPDATE comuni SET santo_patrono=?, patrono_giorno=?, patrono_mese=? WHERE codice_belfiore=?",
            (dati["nome"], dati["giorno"], dati["mese"], belfiore),
        )
        n += cur.rowcount
    conn.commit()
    log.info(f"Patroni caricati: {n} comuni aggiornati")
    return n


def carica_rifiuti(conn: sqlite3.Connection, offline: bool = False) -> int:
    """
    Popola le colonne perc_raccolta_diff / rifiuti_kg_ab / anno_rifiuti
    scaricando il CSV dal Catasto Rifiuti ISPRA (aggiornamento annuale).
    Mappa IstatComune (8 cifre: 2 region + 6 istat) -> codice_istat DB (6 cifre).
    """
    import io as _io

    cache_file = Path("tools/cache/ispra_rifiuti.csv")
    cache_file.parent.mkdir(parents=True, exist_ok=True)

    def _parse_it_float(s) -> float | None:
        if s is None:
            return None
        if isinstance(s, (int, float)):
            import math
            return None if math.isnan(s) else float(s)
        s = str(s).strip().replace(".", "").replace(",", ".").replace("%", "")
        if s in ("-", ""):
            return None
        try:
            return float(s)
        except ValueError:
            return None

    if offline:
        if not cache_file.exists():
            log.warning("Offline: cache ISPRA rifiuti mancante — skip")
            return 0
        raw = cache_file.read_bytes()
    else:
        try:
            raw = scarica_bytes(ISPRA_RIFIUTI_URL)
            cache_file.write_bytes(raw)
        except Exception as e:
            if cache_file.exists():
                log.warning(f"Download ISPRA rifiuti fallito: {e} — uso cache precedente")
                raw = cache_file.read_bytes()
            else:
                log.warning(f"Download ISPRA rifiuti fallito: {e} — skip (nessuna cache)")
                return 0

    lines = raw.decode("utf-8-sig", errors="replace").splitlines()
    # Line 0: title; Line 1: header (25 cols); Line 2+: data with leading tabs + trailing ;
    if len(lines) < 3:
        log.warning("CSV ISPRA rifiuti vuoto o malformato — skip")
        return 0

    header = lines[1] + ";_extra"
    data_lines = [l.lstrip("\t") for l in lines[2:] if l.strip()]
    df = pd.read_csv(
        _io.StringIO("\n".join([header] + data_lines)),
        sep=";",
        dtype=str,
    )
    df.columns = [c.strip() for c in df.columns]
    df = df.apply(lambda col: col.str.strip() if col.dtype == object else col)

    # Mappa: IstatComune (8 cifre, es. '01001001') -> codice_istat (6 cifre, es. '001001')
    df["istat6"] = df["IstatComune"].str[2:]

    n = 0
    for _, row in df.iterrows():
        istat6 = row.get("istat6", "")
        if not istat6 or len(istat6) != 6:
            continue
        perc_rd    = _parse_it_float(row.get("Percentuale RD (%)"))
        tot_ru_t   = _parse_it_float(row.get("Totale RU (t)"))
        ind_t      = _parse_it_float(row.get("Indifferenziato (t)"))
        rd_t       = _parse_it_float(row.get("Totale RD (t)"))
        umido_t    = _parse_it_float(row.get("Frazione umida(1) (t)"))
        carta_t    = _parse_it_float(row.get("Carta e cartone (t)"))
        vetro_t    = _parse_it_float(row.get("Vetro (t)"))
        plastica_t = _parse_it_float(row.get("Plastica (t)"))
        legno_t    = _parse_it_float(row.get("Legno (t)"))
        metallo_t  = _parse_it_float(row.get("Metallo (t)"))
        verde_t    = _parse_it_float(row.get("Verde (t)"))
        raee_t     = _parse_it_float(row.get("RAEE (t)"))
        pop        = _parse_it_float(row.get("Popolazione"))
        kg_ab: float | None = None
        if tot_ru_t is not None and pop and pop > 0:
            kg_ab = round(tot_ru_t * 1000 / pop, 1)
        cur = conn.execute(
            """UPDATE comuni
               SET perc_raccolta_diff=?, rifiuti_kg_ab=?, rifiuti_tot_t=?,
                   rifiuti_ind_t=?, rifiuti_rd_t=?,
                   rd_umido_t=?, rd_carta_t=?, rd_vetro_t=?, rd_plastica_t=?,
                   rd_legno_t=?, rd_metallo_t=?, rd_verde_t=?, rd_raee_t=?,
                   anno_rifiuti=?
               WHERE codice_istat=?""",
            (perc_rd, kg_ab, tot_ru_t, ind_t, rd_t,
             umido_t, carta_t, vetro_t, plastica_t,
             legno_t, metallo_t, verde_t, raee_t,
             ISPRA_RIFIUTI_ANNO, istat6),
        )
        n += cur.rowcount

    conn.commit()
    log.info(f"ISPRA rifiuti: {n} comuni aggiornati (anno {ISPRA_RIFIUTI_ANNO})")
    return n


def carica_arera(conn: sqlite3.Connection, offline: bool = False) -> int:
    """
    Popola le colonne gestore_rifiuti / tari_fissa_mq / tari_variabile_ab / anno_tari
    scaricando il CSV ARERA sui dati TARI per comune.

    Il CSV ARERA ha colonne (nomi approssimativi, verificare dall'effettivo file):
      Codice ISTAT comune, Denominazione comune, Gestore, Quota fissa €/mq, Quota variabile €/ab
    Il join avviene su codice_istat (6 cifre).
    """
    import io as _io

    cache_file = Path("tools/cache/arera_tari.csv")
    cache_file.parent.mkdir(parents=True, exist_ok=True)

    if offline:
        if not cache_file.exists():
            log.warning("Offline: cache ARERA TARI mancante — skip")
            return 0
        raw = cache_file.read_bytes()
    else:
        try:
            raw = scarica_bytes(ARERA_TARI_URL)
            cache_file.write_bytes(raw)
        except Exception as e:
            if cache_file.exists():
                log.warning(f"Download ARERA TARI fallito: {e} — uso cache precedente")
                raw = cache_file.read_bytes()
            else:
                log.warning(f"Download ARERA TARI fallito: {e} — skip (nessuna cache)")
                return 0

    enc = _detect_enc(raw, "latin-1")
    try:
        df = pd.read_csv(_io.BytesIO(raw), sep=";", encoding=enc, dtype=str)
    except Exception as e:
        log.warning(f"Parse CSV ARERA TARI fallito: {e} — skip")
        return 0

    log.info(f"ARERA TARI: {len(df)} righe, colonne: {list(df.columns)}")

    # Ricerca flessibile colonne
    col_istat    = next((c for c in df.columns if "istat" in c.lower() or "cod" in c.lower() and "comune" in c.lower()), None)
    col_gestore  = next((c for c in df.columns if "gestore" in c.lower()), None)
    col_fissa    = next((c for c in df.columns if "fissa" in c.lower() or "mq" in c.lower()), None)
    col_var      = next((c for c in df.columns if "variabile" in c.lower() or "ab" in c.lower() and "quota" in c.lower()), None)

    if col_istat is None:
        log.warning("ARERA TARI: colonna codice ISTAT non trovata — skip")
        return 0

    def _to_float(v) -> float | None:
        try:
            return float(str(v).replace(",", ".").strip())
        except Exception:
            return None

    n = 0
    for _, row in df.iterrows():
        istat_raw = pulisci(row.get(col_istat, ""))
        if not istat_raw:
            continue
        # Normalizza a 6 cifre (ARERA può usare 6 o 9 cifre ISTAT)
        istat6 = istat_raw[-6:].zfill(6) if len(istat_raw) >= 6 else istat_raw.zfill(6)

        gestore   = pulisci(row.get(col_gestore, "")) if col_gestore else None
        fissa     = _to_float(row.get(col_fissa))   if col_fissa  else None
        variabile = _to_float(row.get(col_var))     if col_var    else None

        cur = conn.execute(
            """UPDATE comuni
               SET gestore_rifiuti=?, tari_fissa_mq=?, tari_variabile_ab=?, anno_tari=?
               WHERE codice_istat=?""",
            (gestore or None, fissa, variabile, ARERA_TARI_ANNO, istat6),
        )
        n += cur.rowcount

    conn.commit()
    log.info(f"ARERA TARI: {n} comuni aggiornati (anno {ARERA_TARI_ANNO})")
    return n


def _make_tls_session() -> "requests.Session":
    """Crea una requests.Session con TLS SECLEVEL=1 per siti PA con TLS legacy."""
    import ssl as _ssl
    from requests.adapters import HTTPAdapter
    from urllib3.util.ssl_ import create_urllib3_context

    class _LegacyTLSAdapter(HTTPAdapter):
        def init_poolmanager(self, *args, **kwargs):
            ctx = create_urllib3_context()
            ctx.check_hostname = False
            ctx.verify_mode = _ssl.CERT_NONE
            ctx.set_ciphers("DEFAULT:@SECLEVEL=1")
            kwargs["ssl_context"] = ctx
            super().init_poolmanager(*args, **kwargs)

    session = requests.Session()
    session.mount("https://", _LegacyTLSAdapter())
    return session


def carica_farmacie(conn: sqlite3.Connection, offline: bool = False) -> int:
    """
    Popola la tabella farmacie dall'elenco Ministero della Salute (settimanale).
    Carica solo le farmacie attive (data_fine_validita vuota o '-').
    Il server usa TLS 1.0 legacy — richiede _make_tls_session().
    """
    import io as _io

    cache_file = Path("tools/cache/salute_farmacie.csv")
    cache_file.parent.mkdir(parents=True, exist_ok=True)

    if offline:
        if not cache_file.exists():
            log.warning("Offline: cache farmacie mancante — skip")
            return 0
        raw = cache_file.read_bytes()
    else:
        try:
            session = _make_tls_session()
            log.info(f"Download farmacie: {SALUTE_FARMACIE_URL}")
            r = session.get(SALUTE_FARMACIE_URL, timeout=TIMEOUT_SECONDI)
            r.raise_for_status()
            raw = r.content
            cache_file.write_bytes(raw)
        except Exception as e:
            if cache_file.exists():
                log.warning(f"Download farmacie fallito: {e} — uso cache precedente")
                raw = cache_file.read_bytes()
            else:
                log.warning(f"Download farmacie fallito: {e} — skip (nessuna cache)")
                return 0

    df = pd.read_csv(
        _io.StringIO(raw.decode("latin-1", errors="replace")),
        sep=";",
        dtype=str,
    )
    df.columns = [c.strip() for c in df.columns]
    df = df.apply(lambda col: col.str.strip() if col.dtype == object else col)

    # Solo farmacie attive
    attive = df[
        df["data_fine_validita"].isna()
        | (df["data_fine_validita"] == "")
        | (df["data_fine_validita"] == "-")
    ].copy()

    def _to_float(s):
        try:
            return float(str(s).replace(",", ".")) if s and s not in ("-", "") else None
        except (ValueError, AttributeError):
            return None

    def _to_int(s):
        try:
            return int(s) if s and s not in ("-", "") else None
        except (ValueError, AttributeError):
            return None

    conn.execute("DELETE FROM farmacie")
    righe = []
    for _, row in attive.iterrows():
        cod_comune = row.get("cod_comune", "")
        try:
            cod_comune = str(int(cod_comune)).zfill(6)
        except (ValueError, TypeError):
            cod_comune = None
        frazione = row.get("frazione")
        righe.append((
            _to_int(row.get("cod_farmacia")),
            row.get("descrizione_farmacia") or None,
            row.get("indirizzo") or None,
            row.get("cap") or None,
            row.get("comune") or None,
            frazione if frazione and frazione not in ("-", "") else None,
            row.get("sigla_provincia") or None,
            row.get("provincia") or None,
            row.get("regione") or None,
            cod_comune,
            row.get("descrizione_tipologia") or None,
            _to_float(row.get("latitudine")),
            _to_float(row.get("longitudine")),
        ))

    conn.executemany(
        """INSERT OR REPLACE INTO farmacie
           (cod_farmacia, denominazione, indirizzo, cap, comune, frazione,
            sigla_provincia, provincia, regione, cod_comune_istat,
            tipologia, latitudine, longitudine)
           VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)""",
        righe,
    )
    conn.commit()
    log.info(f"Farmacie caricate: {len(righe)} farmacie attive")
    return len(righe)


def carica_carburanti(conn: sqlite3.Connection, offline: bool = False) -> int:
    """
    Popola la tabella impianti_carburante dall'anagrafica MIMIT (mensile).
    Formato CSV pipe-delimited, encoding latin-1.
    Colonne: idImpianto|Gestore|Bandiera|Tipo Impianto|Nome Impianto|Indirizzo|Comune|Provincia|Latitudine|Longitudine
    """
    import io as _io

    cache_file = Path("tools/cache/mimit_impianti.csv")
    cache_file.parent.mkdir(parents=True, exist_ok=True)

    if offline:
        if not cache_file.exists():
            log.warning("Offline: cache MIMIT impianti mancante — skip")
            return 0
        raw = cache_file.read_bytes()
    else:
        try:
            raw = scarica_bytes(MIMIT_IMPIANTI_URL)
            cache_file.write_bytes(raw)
        except Exception as e:
            if cache_file.exists():
                log.warning(f"Download MIMIT impianti fallito: {e} — uso cache precedente")
                raw = cache_file.read_bytes()
            else:
                log.warning(f"Download MIMIT impianti fallito: {e} — skip (nessuna cache)")
                return 0

    lines = raw.decode("latin-1", errors="replace").splitlines()
    if len(lines) < 3:
        log.warning("CSV MIMIT impianti vuoto — skip")
        return 0

    # Line 0: "Estrazione del YYYY-MM-DD", Line 1: header, Line 2+: data
    header = lines[1]
    data_lines = lines[2:]
    df = pd.read_csv(
        _io.StringIO("\n".join([header] + data_lines)),
        sep="|",
        dtype=str,
    )
    df.columns = [c.strip() for c in df.columns]
    df = df.apply(lambda col: col.str.strip() if col.dtype == object else col)

    def _to_float(s):
        try:
            return float(s.replace(",", ".")) if s else None
        except (ValueError, AttributeError):
            return None

    def _to_int(s):
        try:
            return int(s) if s else None
        except (ValueError, AttributeError):
            return None

    conn.execute("DELETE FROM impianti_carburante")
    righe = []
    for _, row in df.iterrows():
        righe.append((
            _to_int(row.get("idImpianto")),
            row.get("Gestore") or None,
            row.get("Bandiera") or None,
            row.get("Tipo Impianto") or None,
            row.get("Nome Impianto") or None,
            row.get("Indirizzo") or None,
            row.get("Comune") or None,
            row.get("Provincia") or None,
            _to_float(row.get("Latitudine")),
            _to_float(row.get("Longitudine")),
        ))

    conn.executemany(
        """INSERT OR REPLACE INTO impianti_carburante
           (id_impianto, gestore, bandiera, tipo_impianto, nome_impianto,
            indirizzo, comune, sigla_provincia, latitudine, longitudine)
           VALUES (?,?,?,?,?,?,?,?,?,?)""",
        righe,
    )
    conn.commit()
    log.info(f"Impianti carburante caricati: {len(righe)} impianti")
    return len(righe)


def carica_dati_default(conn: sqlite3.Connection):
    conn.executemany(
        "INSERT OR REPLACE INTO operatori_mobili (prefisso, nome_operatore, tecnologia) VALUES (?,?,?)",
        OPERATORI_DEFAULT,
    )
    conn.executemany(
        "INSERT OR REPLACE INTO prefissi_telefonici (prefisso, tipo, area_geografica) VALUES (?,?,?)",
        PREFISSI_DEFAULT,
    )
    conn.commit()
    log.info("Dati default (operatori, prefissi) caricati.")


# ── FTS5 ──────────────────────────────────────────────────────────────────────

def ricostruisci_fts(conn: sqlite3.Connection):
    log.info("Ricostruzione indice FTS5...")
    conn.execute("INSERT INTO comuni_fts(comuni_fts) VALUES('rebuild')")
    conn.commit()
    log.info("FTS5 ricostruito.")


# ── Changelog ─────────────────────────────────────────────────────────────────

def genera_changelog(conn: sqlite3.Connection, output_path: Path):
    def count(q): return conn.execute(q).fetchone()[0]
    changelog = f"""## Atlante Data Update — {VERSIONE_DATI}

### Statistiche Database
- Comuni attivi: {count("SELECT COUNT(*) FROM comuni WHERE is_attivo=1")}
- Comuni storici/soppressi: {count("SELECT COUNT(*) FROM comuni WHERE is_attivo=0")}
- Variazioni amministrative: {count("SELECT COUNT(*) FROM variazioni_storiche")}
- Record CAP: {count("SELECT COUNT(*) FROM cap")}
- Banche (ABI): {count("SELECT COUNT(*) FROM banche")}
- Codici ATECO: {count("SELECT COUNT(*) FROM ateco")}
- Enti IPA/SdI: {count("SELECT COUNT(*) FROM ipa_enti")}
- Aggiornato: {datetime.now().isoformat()}

### Fonti
- ISTAT Open Data — Codici Unità Amministrative
- GeoNames — CAP e coordinate WGS84
- Banca d'Italia — Codici ABI/BIC
- ISTAT — Codici ATECO 2007
- IndicePA — Enti PA e codici SdI
- GitHub andrea-e — Zone climatiche DPR 412/93
- INGV/Protezione Civile — Zone sismiche

"""
    changelog_file = output_path.parent.parent / "CHANGELOG.md"
    existing = changelog_file.read_text(encoding="utf-8") if changelog_file.exists() else ""
    changelog_file.write_text(changelog + existing, encoding="utf-8")
    log.info(f"Changelog aggiornato: {changelog_file}")


# ── Main ──────────────────────────────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(description="Genera il database Atlante da open data italiani")
    parser.add_argument("--output", "-o", default="src/Italy.Core/data/italy.db")
    parser.add_argument("--offline", action="store_true",
                        help="Usa CSV locali in tools/cache/ invece di scaricare")
    args = parser.parse_args()

    output    = Path(args.output)
    cache_dir = Path("tools/cache")
    output.parent.mkdir(parents=True, exist_ok=True)
    cache_dir.mkdir(exist_ok=True)

    if output.exists():
        output.unlink()
        log.info(f"Database precedente rimosso: {output}")

    conn = sqlite3.connect(str(output))
    try:
        conn.executescript(SCHEMA_SQL)
        log.info("Schema creato.")

        # 1. Comuni ISTAT
        df = tenta_scarica_csv(ISTAT_COMUNI_URL, cache_dir / "comuni.csv",
                               args.offline, sep=";")
        if df is not None:
            carica_comuni(conn, df)
        else:
            log.error("Comuni ISTAT non disponibili.")

        # 2. Variazioni storiche (ZIP con CSV interno)
        df = tenta_scarica_zip_csv_interno(
            ISTAT_VARIAZIONI_URL, cache_dir / "variazioni.csv",
            args.offline, sep=";", pattern=".csv"
        )
        if df is not None:
            carica_variazioni_storiche(conn, df)
        else:
            log.warning("Variazioni storiche non disponibili.")

        # 3. GeoNames CAP + coordinate
        df = tenta_scarica_zip_tsv(GEONAMES_URL, cache_dir / "geonames_it.tsv",
                                   args.offline, pattern="IT.txt")
        if df is not None:
            carica_geonames(conn, df)
        else:
            log.warning("GeoNames non disponibile.")

        # 4 & 5. Zone sismiche — CSV con sep=; (contiene anche dati per future zone climatiche)
        df = tenta_scarica_csv(ZONE_SISMICHE_URL, cache_dir / "zone_sismiche.csv",
                               args.offline, sep=";", encoding="utf-8")
        if df is not None:
            carica_zone_sismiche(conn, df)
        else:
            log.warning("Zone sismiche non disponibili.")

        # Zone climatiche — CSV GitHub ferdi2005 (dati DPR 412/93)
        df = tenta_scarica_csv(ZONE_CLIMATICHE_URL, cache_dir / "zone_climatiche.csv",
                               args.offline, sep=";", encoding="utf-8")
        if df is not None:
            carica_zone_climatiche(conn, df)
        else:
            log.warning("Zone climatiche non disponibili.")

        # 6. Banche — Albo Banche Banca d'Italia
        df_banche = tenta_scarica_csv(BANCA_ITALIA_URL, cache_dir / "banche_bi.csv",
                                      args.offline, sep=";", encoding="latin-1")
        df_bic = tenta_scarica_csv(BANCA_ITALIA_BIC_URL, cache_dir / "banche_bi_bic.csv",
                                   args.offline, sep=";", encoding="latin-1")
        if df_banche is not None:
            carica_banche_bancaditalia(conn, df_banche, df_bic)
        else:
            log.warning("Albo Banche Banca d'Italia non disponibile.")

        # 7. ATECO 2007 — XLSX ISTAT
        df = tenta_scarica_xlsx(ATECO_XLSX_URL, cache_dir / "ateco.xlsx", args.offline)
        if df is not None:
            carica_ateco(conn, df)
        else:
            log.warning("ATECO non disponibile.")

        # 8. IndicePA — XLSX
        df = tenta_scarica_xlsx(IPA_XLSX_URL, cache_dir / "ipa.xlsx", args.offline)
        if df is not None:
            carica_ipa(conn, df)
        else:
            log.warning("IndicePA non disponibile.")

        # 9. Aggregazioni sovracomunali — da CSV ISTAT comuni (già scaricato al passo 1)
        df_comuni_reload = tenta_scarica_csv(ISTAT_COMUNI_URL, cache_dir / "comuni.csv",
                                             True, sep=";")  # sempre da cache, già scaricato
        if df_comuni_reload is not None:
            carica_aggregazioni_sovracomunali(conn, df_comuni_reload)
        else:
            log.warning("Aggregazioni sovracomunali non disponibili (CSV comuni mancante).")

        # 10. Aree Interne ISTAT — XLSX con header a riga 2, join su codice_belfiore
        aree_interne_xlsx = cache_dir / "aree_interne.xlsx"
        carica_aree_interne(conn, aree_interne_xlsx)

        # 11a. Superficie comuni — XLSX ISTAT comuni_geo, sheet "Dati comunali"
        comuni_geo_xlsx = cache_dir / "comuni_geo.xlsx"
        carica_comuni_geo(conn, comuni_geo_xlsx)

        # 11b. Popolazione ISTAT — XLSX per comune (join su denominazione)
        popolazione_xlsx = cache_dir / "popolazione.xlsx"
        carica_popolazione(conn, popolazione_xlsx)

        # 12. ASL Ministero della Salute — XLSX (può essere .xls o .xlsx)
        asl_xlsx = cache_dir / "asl.xlsx"
        carica_asl(conn, asl_xlsx)

        # 14. Santi Patroni — da tools/patroni.json (generato da scrape_patroni.py)
        carica_patroni(conn, Path("tools/patroni.json"))

        # 15. PEC Comuni — da tools/pec_comuni.json (da IndicePA, aggiornare periodicamente)
        carica_pec(conn, Path("tools/pec_comuni.json"))

        # 16. Rifiuti urbani — ISPRA Catasto Rifiuti (CSV annuale per comune)
        carica_rifiuti(conn, offline=args.offline)

        # 17. Impianti carburante — MIMIT anagrafica (CSV mensile)
        carica_carburanti(conn, offline=args.offline)

        # 18. Farmacie attive — Ministero della Salute (CSV settimanale)
        carica_farmacie(conn, offline=args.offline)

        # Telefonia — operatori mobili + prefissi emergenza/tollFree
        carica_dati_default(conn)

        # 13. Prefissi geografici provinciali
        carica_prefissi_geografici(conn)

        # FTS5
        ricostruisci_fts(conn)

        # Metadati
        for k, v in [
            ("versione_dati",       VERSIONE_DATI),
            ("data_aggiornamento",  datetime.now().isoformat()),
            ("fonte",               "ISTAT, GeoNames, BancaItalia, ATECO, IndicePA, INGV"),
        ]:
            conn.execute("INSERT OR REPLACE INTO meta VALUES (?,?)", (k, v))
        conn.commit()

        log.info("VACUUM...")
        conn.execute("VACUUM")
        conn.close()

        size_mb = output.stat().st_size / 1_048_576
        log.info(f"Database generato: {output} ({size_mb:.1f} MB)")

        conn2 = sqlite3.connect(str(output))
        genera_changelog(conn2, output)
        conn2.close()

        log.info("✓ Build Atlante completata con successo.")
        return 0

    except Exception as e:
        log.error(f"Errore fatale: {e}", exc_info=True)
        conn.close()
        return 1


if __name__ == "__main__":
    sys.exit(main())
