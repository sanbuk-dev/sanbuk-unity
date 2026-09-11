#!/usr/bin/env python3
"""
Check every Java name this package says out loud against the .aar it ships.

Nothing else can. The C# compiles whatever string you put in an
AndroidJavaObject, so a wrong class name, a renamed getter or a constructor
with the wrong number of arguments is not a build error — it is a crash on a
player's phone, and only on the screen that happens to show an ad.

This already earned its place: Sanbuk.Init built a SanbukConfig with
(mediaCode, debug), but the generated constructor is (mediaCode, trackerUrl,
debug) — Kotlin fills default arguments from the right, so the two-argument
form the shell assumed never existed. It compiled cleanly.

Run:  python3 Tests/verify_jni.py
"""

import pathlib
import re
import subprocess
import sys
import tempfile
import zipfile

ROOT = pathlib.Path(__file__).resolve().parent.parent
SOURCES = sorted(ROOT.glob("Runtime/*.cs")) + sorted(ROOT.glob("Runtime/Internal/*.cs"))

# Which class each method literal is called on. Written by hand because the
# receiver is not recoverable from the C# text — but nothing is taken on trust:
# every entry is checked against the .aar below, and every literal in the C# has
# to appear here, so a new call cannot be added without being declared.
OWNERS = {
    "ir.sanbuk.sdk.Sanbuk": ["init", "loadAd"],
    "ir.sanbuk.sdk.SanbukAd": [
        "getFormat", "getCampaignId", "getHeadline", "getBody",
        "getCallToAction", "getImageUrl", "getLogoUrl", "getDisclosureLabel",
        "getBrandColor", "recordImpression", "click",
    ],
    "ir.sanbuk.sdk.SanbukAdView": ["load"],
    "ir.sanbuk.sdk.SanbukFullscreen": ["show"],
    "ir.sanbuk.sdk.SanbukFullscreen$Callbacks": [
        "onShown", "onClosed", "onReward", "onRefused",
    ],
}

# Called on classes that are not ours, so the .aar cannot answer for them.
# Listed rather than skipped silently, so the set stays small and deliberate.
FOREIGN = {
    "addContentView", "runOnUiThread",   # android.app.Activity
    "setVisibility", "getParent",        # android.view.View
    "removeView",                        # android.view.ViewGroup
    "intValue",                          # java.lang.Integer
}

STATIC_FIELDS = {
    "ir.sanbuk.sdk.Sanbuk": ["INSTANCE"],
}


def read_sources():
    return {p: p.read_text() for p in SOURCES}


def javap(classes_dir, binary_name):
    path = classes_dir / (binary_name.replace(".", "/") + ".class")
    # A nested class is Outer$Inner on disk; the replace above breaks that back
    # apart, so rebuild it.
    if not path.exists() and "$" in binary_name:
        outer, inner = binary_name.rsplit("$", 1)
        path = classes_dir / (outer.replace(".", "/") + "$" + inner + ".class")
    if not path.exists():
        return None

    return subprocess.run(
        ["javap", "-p", str(path)], capture_output=True, text=True, check=True
    ).stdout


def main():
    aars = sorted(ROOT.glob("Runtime/Plugins/Android/*.aar"))
    if len(aars) != 1:
        print(f"FAIL: expected exactly one .aar, found {len(aars)}")
        return 1

    failures = []
    sources = read_sources()
    blob = "\n".join(sources.values())

    with tempfile.TemporaryDirectory() as tmp:
        tmp = pathlib.Path(tmp)
        with zipfile.ZipFile(aars[0]) as z:
            z.extract("classes.jar", tmp)
        with zipfile.ZipFile(tmp / "classes.jar") as z:
            z.extractall(tmp / "classes")
        classes = tmp / "classes"

        # 1. Every ir.sanbuk class named in the C# exists in the .aar.
        named = set(re.findall(r'"(ir\.sanbuk[A-Za-z0-9._$]*)"', blob))
        if not named:
            failures.append("no ir.sanbuk class literals found — the regex stopped matching")
        for cls in sorted(named):
            if javap(classes, cls) is None:
                failures.append(f"class named in C# but absent from the .aar: {cls}")

        # 2. Every method literal is declared above, and exists where declared.
        called = set(re.findall(r'\.Call(?:Static)?(?:<[^>]+>)?\("(\w+)"', blob))
        if not called:
            failures.append("no method literals found — the regex stopped matching")
        declared = {m for ms in OWNERS.values() for m in ms} | FOREIGN
        for method in sorted(called - declared):
            failures.append(f"method called in C# but not declared in OWNERS: {method}")

        for cls, methods in OWNERS.items():
            dump = javap(classes, cls)
            if dump is None:
                failures.append(f"OWNERS names a class the .aar does not have: {cls}")
                continue
            for method in methods:
                if not re.search(rf"\b{re.escape(method)}\(", dump):
                    failures.append(f"{cls} has no method {method}")

        # 3. Static fields read with GetStatic.
        for cls, fields in STATIC_FIELDS.items():
            dump = javap(classes, cls) or ""
            for field in fields:
                if not re.search(rf"\b{re.escape(field)};", dump):
                    failures.append(f"{cls} has no static field {field}")

        # 4. Constructor arity. The bug that prompted this file.
        for match in re.finditer(
            r'new AndroidJavaObject\(\s*"(ir\.sanbuk[\w.$]+)"([^;]*?)\)\s*;', blob, re.S
        ):
            cls, rest = match.group(1), match.group(2)
            arity = len([a for a in rest.split(",") if a.strip()])
            dump = javap(classes, cls)
            if dump is None:
                continue
            simple = cls.split(".")[-1]
            arities = {
                len([a for a in sig.split(",") if a.strip()])
                for sig in re.findall(rf"public {re.escape(cls)}\(([^)]*)\)", dump)
            }
            if arity not in arities:
                failures.append(
                    f"{simple} constructed with {arity} argument(s); "
                    f"the .aar offers {sorted(arities)}"
                )

    if failures:
        print(f"FAIL ({len(failures)}):")
        for f in failures:
            print("  -", f)
        return 1

    print(f"OK — {len(named)} classes, {len(called)} method names, "
          f"{sum(len(v) for v in STATIC_FIELDS.values())} static fields "
          f"checked against {aars[0].name}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
