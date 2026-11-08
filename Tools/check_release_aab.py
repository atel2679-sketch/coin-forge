"""IC-06 release check: proves dev-only code and QA logging are not in a release AAB/APK.

Usage:  python -I Tools/check_release_aab.py Builds/CoinForge-1.0.0-1.aab [--expect-app-id ca-app-pub-...~...]

Scans IL2CPP metadata (type/method/string names) and libil2cpp.so for symbols that only exist in
#if DEVELOPMENT_BUILD || UNITY_EDITOR code, checks that the code that must ship is present, and reports
the AdMob App ID found in the manifest. Exit code 1 if anything is wrong.
"""
import re
import sys
import zipfile

MUST_BE_ABSENT = [
    # debug panel, perf probe, playtest bot
    "CoinForgeDebugPanel", "HoldToOpen", "CoinForgePerfProbe", "CoinForgePlaytestBot",
    # dev hooks
    "DebugAddCoins", "DebugSkip", "DebugSaveBlocked", "DebugResetConsent", "DebugEeaKey", "dbg_ump_eea",
    # QA log tags and panel text
    "IC04_PERF", "IC05_PLAYTEST", "timewarp_first_shown", "IC-04 DEBUG", "Playtest bot",
    # store-screenshot build only
    "StoreShots", "-storeshot",
]
MUST_BE_PRESENT = ["GameState", "RewardFlow", "StartAfterConsent", "AdConfig", "ReleaseLogging", "Privacy settings"]
TEST_APP_ID = b"ca-app-pub-3940256099942544~3347511713"


def main(path, expect_app_id=None):
    with zipfile.ZipFile(path) as z:
        names = z.namelist()
        prefix = "base/" if any(n.startswith("base/") for n in names) else ""
        meta = z.read(prefix + "assets/bin/Data/Managed/Metadata/global-metadata.dat")
        lib = z.read(prefix + "lib/arm64-v8a/libil2cpp.so")
        manifest = z.read(prefix + ("manifest/AndroidManifest.xml" if prefix else "AndroidManifest.xml"))
        abis = sorted({n.split("/")[-2] for n in names if n.endswith(".so")})

    ok = True
    print(f"{path}\n  ABIs: {', '.join(abis)}")
    for sym in MUST_BE_ABSENT:
        b = sym.encode()
        hit = [w for w, blob in (("metadata", meta), ("libil2cpp", lib)) if b in blob]
        print(f"  {'FAIL present' if hit else 'ok   absent '}  {sym}  {' '.join(hit)}")
        ok &= not hit
    for sym in MUST_BE_PRESENT:
        present = sym.encode() in meta
        print(f"  {'ok   present' if present else 'FAIL missing'}  {sym}")
        ok &= present

    # Binary XML (APK) stores strings as UTF-16, proto XML (AAB) as UTF-8: search both.
    ids = set(re.findall(rb"ca-app-pub-\d{16}~\d{10}", manifest))
    ids |= {m.replace(b"\x00", b"") for m in re.findall(rb"c\x00a\x00-\x00a\x00p\x00p\x00-\x00p\x00u\x00b\x00(?:-\x00|[0-9~]\x00)+", manifest)}
    app_ids = sorted(i.decode() for i in ids)
    print(f"  manifest App ID: {', '.join(app_ids) or 'none'}")
    if expect_app_id:
        good = app_ids == [expect_app_id]
        print(f"  {'ok  ' if good else 'FAIL'} expected App ID {expect_app_id}")
        ok &= good
    print("RESULT:", "PASS" if ok else "FAIL")
    return 0 if ok else 1


if __name__ == "__main__":
    args = sys.argv[1:]
    expect = None
    if "--expect-app-id" in args:
        i = args.index("--expect-app-id")
        expect = args[i + 1]
        del args[i:i + 2]
    sys.exit(main(args[0], expect))
