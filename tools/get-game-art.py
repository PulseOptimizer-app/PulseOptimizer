"""
Downloads portrait cover art for the games Pulse shows (its game checklists and the "Find something new" list)
from Steam's public store, at build time only. The images are embedded in PulseOptimizer.exe so the app itself
never goes online. Game art belongs to its publishers and is used only to identify each game.

Usage: python tools/get-game-art.py <out-dir>
Games that aren't on Steam (VALORANT, Fortnite, Roblox, ...) are skipped; the app uses their installed icon instead.
"""
import json, os, re, sys, time, urllib.parse, urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "src", "PulseOptimizer", "Core", "Games")
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "src", "PulseOptimizer", "Assets", "Games")
UA = {"User-Agent": "PulseOptimizer-build"}
# Names Steam lists differently.
ALIASES = {
    "Call of Duty: Warzone": "Call of Duty",
    "Overwatch 2": "Overwatch",
    "Rainbow Six Siege": "Tom Clancy's Rainbow Six Siege",
}
SKIP = {"Any other game"}

def norm(s): return re.sub(r"[^a-z0-9]", "", s.lower())

def key_for(name): return norm(name)

def get(url, binary=False):
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=20) as r:
        data = r.read()
    return data if binary else json.loads(data.decode("utf-8"))

def find_app(name):
    term = ALIASES.get(name, name)
    res = get("https://store.steampowered.com/api/storesearch/?l=english&cc=US&term=" + urllib.parse.quote(term))
    want = norm(term)
    for item in res.get("items", []):
        if item.get("type") != "app": continue
        got = norm(item.get("name", ""))
        if got == want or got.startswith(want) and len(got) - len(want) <= 3:
            return item["id"]
    return None

def art(app_id):
    for url in (f"https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/{app_id}/library_600x900.jpg",
                f"https://cdn.akamai.steamstatic.com/steam/apps/{app_id}/library_600x900.jpg"):
        try:
            data = get(url, binary=True)
            if len(data) > 5000: return data
        except Exception:
            pass
    return None

def names():
    out = []
    cat = open(os.path.join(SRC, "GameCatalog.cs"), encoding="utf-8").read()
    out += re.findall(r'new\("[a-z0-9]+", "([^"]+)", "', cat)
    sug = open(os.path.join(SRC, "Suggestions.cs"), encoding="utf-8").read()
    out += re.findall(r'^\s*new\("([^"]+)", "', sug, re.M)
    return list(dict.fromkeys(out))

def main():
    os.makedirs(OUT, exist_ok=True)
    found = 0
    for name in names():
        if name in SKIP: continue
        path = os.path.join(OUT, key_for(name) + ".jpg")
        try:
            app = find_app(name)
            data = art(app) if app else None
            if data:
                open(path, "wb").write(data); found += 1
                print(f"ok    {name} (app {app})")
            else:
                print(f"skip  {name} (not on Steam)")
        except Exception as e:
            print(f"skip  {name} ({e})")
        time.sleep(0.4)
    print(f"{found} covers saved to {OUT}")

if __name__ == "__main__":
    main()
