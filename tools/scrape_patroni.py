"""
Scarica i santi patroni da santiebeati.it/patronati e li mappa
sui codici Belfiore del DB italy.db.

Struttura HTML per ogni comune:
  <font color="#ff3300"><b>Nome Comune</b></font>
  <br>
  <a href="/dettaglio/XXXXX"><FONT SIZE="-1"><b>Nome Santo</b></FONT></a>
  <IMG ...>
  <FONT SIZE="-2" COLOR="#ff3300">giorno mese</FONT>

Output:
  tools/patroni.json           (codice_belfiore -> {comune, nome, giorno, mese})
  tools/patroni_non_trovati.txt

Uso:
    cd C:/Sorgenti/ItalyCore && python tools/scrape_patroni.py
"""

import re
import time
import json
import sqlite3
import unicodedata
import requests
from bs4 import BeautifulSoup

DB_PATH = "src/Italy.Core/data/italy.db"
BASE_URL = "https://www.santiebeati.it/patronati/{lettera}/"
LETTERE = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
HEADERS = {"User-Agent": "Mozilla/5.0 (research; patroni comuni italiani)"}

MESI = {
    "gennaio": 1, "febbraio": 2, "marzo": 3, "aprile": 4,
    "maggio": 5, "giugno": 6, "luglio": 7, "agosto": 8,
    "settembre": 9, "ottobre": 10, "novembre": 11, "dicembre": 12,
}


# ---------------------------------------------------------------------------
def carica_comuni_db():
    conn = sqlite3.connect(DB_PATH)
    cur = conn.cursor()
    cur.execute("""
        SELECT codice_belfiore, denominazione, denominazione_alt, sigla_provincia
        FROM comuni WHERE is_attivo = 1
    """)
    rows = cur.fetchall()
    conn.close()

    lookup = {}
    for belfiore, denom, denom_alt, prov in rows:
        for nome in filter(None, [denom, denom_alt]):
            key = normalizza(nome)
            lookup.setdefault(key, []).append((belfiore, denom, prov or ""))
    return lookup


def normalizza(s):
    s = unicodedata.normalize("NFD", s)
    s = "".join(c for c in s if unicodedata.category(c) != "Mn")
    return re.sub(r"[\s'\-]+", " ", s.lower()).strip()


def parse_data(data_str):
    """Ritorna (giorno, mese) o None se data mobile/vaga."""
    data_str = data_str.lower().strip()
    m = re.match(r"(\d{1,2})\s+(\w+)", data_str)
    if not m:
        return None
    giorno = int(m.group(1))
    mese = MESI.get(m.group(2))
    if not mese or not (1 <= giorno <= 31):
        return None
    return (giorno, mese)


def pulisci_santo(nome):
    """Rimuovi titoli verbosi dopo parole chiave."""
    nome = re.sub(
        r"\s+(Vescovo|Martire|Apostolo|Abate|Papa|Confessore|Diacono|"
        r"Vergine|Beato|Beata|Sacerdote|Eremita|Monaco|Arcivescovo|"
        r"Religioso|Religiosa|Pellegrino)\b.*$",
        "", nome, flags=re.IGNORECASE
    )
    return nome.strip()


# ---------------------------------------------------------------------------
def estrai_righe(html):
    """
    I font rossi per i comuni hanno color="#ff3300", non size, non dentro <a>.
    I loro sibling diretti sono: <br>, <a>(santo), <img>, <font size=-2>(data).
    Le NavigableString vuote tra i sibling vengono restituite come nodi None.name.
    """
    soup = BeautifulSoup(html, "html.parser")
    risultati = []

    for cf in soup.find_all("font", {"color": re.compile(r"ff3300", re.I)}):
        # Escludi font con size (quelli sono le date)
        if cf.get("size"):
            continue
        # Escludi font dentro <a> (quelli sono i santi)
        if cf.find_parent("a"):
            continue
        b = cf.find("b", recursive=False)
        if not b:
            continue

        comune_raw = b.get_text(strip=True)
        if not comune_raw:
            continue

        m_prov = re.search(r"\(([A-Z]{2})\)\s*$", comune_raw)
        prov_hint = m_prov.group(1) if m_prov else None
        comune_nome = re.sub(r"\s*\([^)]+\)\s*$", "", comune_raw).strip()

        # Raccogli tutti i sibling fino al prossimo comune
        # Struttura attesa: <br> <a> <img> <font size=-2>
        link_santo = None
        data_font = None

        for sib in cf.next_siblings:
            tag = getattr(sib, "name", None)
            if tag is None:
                # NavigableString (whitespace)
                continue
            if tag == "br":
                continue
            if tag == "img":
                continue
            if tag == "a" and link_santo is None:
                link_santo = sib
                continue
            if tag == "font" and sib.get("size") == "-2":
                data_font = sib
                break
            # Qualsiasi altro tag (p, font senza size, ecc.) → fine del gruppo
            break

        if not link_santo or not data_font:
            continue

        santo_b = link_santo.find("b")
        santo_nome = (santo_b.get_text(strip=True) if santo_b
                      else link_santo.get_text(strip=True))
        santo_nome = re.sub(r"\s+", " ", santo_nome).strip()
        data_str = data_font.get_text(strip=True)

        if santo_nome and data_str:
            risultati.append({
                "comune": comune_nome,
                "prov_hint": prov_hint,
                "santo": santo_nome,
                "data_raw": data_str,
            })

    return risultati


# ---------------------------------------------------------------------------
def trova_belfiore(comune_nome, prov_hint, lookup):
    key = normalizza(comune_nome)
    candidati = lookup.get(key, [])
    if not candidati:
        return None
    if len(candidati) == 1:
        return candidati[0][0], candidati[0][1]
    if prov_hint:
        for belfiore, denom, prov in candidati:
            if prov.upper() == prov_hint.upper():
                return belfiore, denom
    return candidati[0][0], candidati[0][1]


# ---------------------------------------------------------------------------
def main():
    print("Carico comuni dal DB...")
    lookup = carica_comuni_db()
    print(f"  {len(lookup)} chiavi nel lookup")

    tutte_righe = []
    for lettera in LETTERE:
        url = BASE_URL.format(lettera=lettera)
        print(f"  Scarico {url} ...", end=" ", flush=True)
        try:
            r = requests.get(url, headers=HEADERS, timeout=20)
            r.encoding = r.apparent_encoding
            righe = estrai_righe(r.text)
            print(f"{len(righe)} righe")
            tutte_righe.extend(righe)
        except Exception as e:
            print(f"ERRORE: {e}")
        time.sleep(0.8)

    print(f"\nTotale righe raw: {len(tutte_righe)}")

    patroni = {}
    non_trovati = []

    for row in tutte_righe:
        data = parse_data(row["data_raw"])
        if not data:
            continue

        giorno, mese = data
        result = trova_belfiore(row["comune"], row["prov_hint"], lookup)
        if result is None:
            non_trovati.append(
                f"{row['comune']} ({row['prov_hint']}) — {row['santo']} — {row['data_raw']}"
            )
            continue

        belfiore, denom = result
        if belfiore not in patroni:
            patroni[belfiore] = {
                "comune": denom,
                "nome": pulisci_santo(row["santo"]),
                "giorno": giorno,
                "mese": mese,
            }

    print(f"Comuni mappati: {len(patroni)}")
    print(f"Non trovati o data mobile: {len(non_trovati)}")

    with open("tools/patroni.json", "w", encoding="utf-8") as f:
        json.dump(patroni, f, ensure_ascii=False, indent=2)

    with open("tools/patroni_non_trovati.txt", "w", encoding="utf-8") as f:
        f.write("\n".join(non_trovati))

    print("\nFile salvati: tools/patroni.json  tools/patroni_non_trovati.txt")
    print("\nPrime 20 entry:")
    for k, v in list(patroni.items())[:20]:
        print(f"  {k}: {v['comune']} — {v['nome']} ({v['giorno']}/{v['mese']})")


if __name__ == "__main__":
    main()
