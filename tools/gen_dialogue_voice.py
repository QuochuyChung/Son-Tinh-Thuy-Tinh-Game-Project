# Generates dialogue voice clips with edge-tts into Assets/Resources/Audio/Voice/<Speaker>/.
# Clip file name = sha1(line text)[:16] so DialogueVoicePlayer can look it up at runtime.
# Per-line speed/volume varies deterministically (hash jitter + punctuation rules).
# Run: uv run --with edge-tts,pyyaml python tools/gen_dialogue_voice.py [--force]
# --force re-synthesises existing clips (needed after changing SPEAKERS rates).
# --only <Folder> synthesises just one speaker folder (skips clips already on disk;
# add --force to also re-synth existing ones), e.g. --only HungVuong.
import asyncio
import hashlib
import re
import sys
from collections import Counter
from pathlib import Path

import edge_tts
import yaml

if sys.stdout.encoding and sys.stdout.encoding.lower() != "utf-8":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[1]
DIALOGUE_DIR = ROOT / "Assets" / "Data" / "Dialogue"
OUT_DIR = ROOT / "Assets" / "Resources" / "Audio" / "Voice"

MALE = "vi-VN-NamMinhNeural"
FEMALE = "vi-VN-HoaiMyNeural"

# speaker -> (folder, edge-tts voice, base rate). Unity adds per-character pitch/volume on top.
SPEAKERS = {
    "Sơn Tinh": ("SonTinh", MALE, "+10%"),
    "Thủy Tinh": ("ThuyTinh", MALE, "+16%"),
    "Hùng Vương": ("HungVuong", MALE, "+8%"),
    "Mị Nương": ("MiNuong", "fr-FR-VivienneMultilingualNeural", "+4%"),
    "Người chăn ngựa": ("NguoiChanNgua", MALE, "+4%"),
    "Thủ lĩnh Ninja": ("ThuLinhNinja", MALE, "+6%"),
    "Ninja": ("Ninja", MALE, "+20%"),
    "Voi Chín Ngà": ("VoiChinNga", MALE, "-8%"),
    "Dẫn chuyện": ("Narrator", MALE, "+4%"),  # narrator: deep, unhurried fairy-tale pace
}


def line_prosody(base_rate: str, text: str) -> tuple:
    """Per-line dynamic prosody: deterministic rate jitter from the text hash
    plus punctuation rules, so no two lines are delivered at exactly the same speed."""
    jitter = (int(hashlib.sha1(text.encode("utf-8")).hexdigest()[:4], 16) % 7) - 3  # -3..+3 %
    rate = int(base_rate.rstrip("%")) + jitter
    trimmed = text.rstrip()
    if trimmed.endswith("?"):
        rate += 4
    elif trimmed.endswith("!"):
        rate += 2
    rate = max(-40, min(60, rate))
    volume = "+10%" if trimmed.endswith("!") else "+0%"
    return f"{'+' if rate >= 0 else ''}{rate}%", volume

# Lines built in code (not in .asset files). Text must match the C# string exactly.
EXTRA_LINES = [
    ("Dẫn chuyện", "Giữa thung lũng đá, Voi Chín Ngà đứng chắn lối. Mỗi nhịp thở của linh tượng làm mặt đất khẽ rung chuyển."),
    ("Dẫn chuyện", "Chín chiếc ngà rực sáng. Linh tượng hạ thấp thân mình, sẵn sàng tung ra những đòn rung núi chuyển rừng."),
    ("Voi Chín Ngà", "Kẻ phàm nào dám bước vào lãnh địa của ta?"),
    ("Sơn Tinh", "Ta là Sơn Tinh. Ta đến tìm Voi Chín Ngà làm sính lễ dâng Vua Hùng."),
    ("Voi Chín Ngà", "Sính lễ không dành cho kẻ chỉ biết khoe sức. Hãy chứng minh ngươi đủ bản lĩnh bảo vệ Mị Nương."),
    ("Sơn Tinh", "Ta chấp nhận thử thách. Xin hãy xuất chiêu!"),
]


def clip_name(text: str) -> str:
    return hashlib.sha1(text.encode("utf-8")).hexdigest()[:16]


def load_asset_lines(path: Path):
    cleaned = []
    for line in path.read_text(encoding="utf-8").splitlines():
        if line.startswith("%"):
            continue
        cleaned.append(re.sub(r"^--- !u!\d+ &\d+", "---", line))
    doc = yaml.safe_load("\n".join(cleaned)) or {}
    root = doc.get("MonoBehaviour") or doc
    return root.get("lines") or []


async def synth(sem, folder, voice, rate, text, stats, force):
    out = OUT_DIR / folder / (clip_name(text) + ".mp3")
    if not force and out.exists() and out.stat().st_size > 2000:
        stats["skipped"] += 1
        return
    out.parent.mkdir(parents=True, exist_ok=True)
    line_rate, line_volume = line_prosody(rate, text)
    async with sem:
        for attempt in range(4):
            try:
                await edge_tts.Communicate(text, voice, rate=line_rate, volume=line_volume).save(str(out))
                stats["made"] += 1
                return
            except Exception as exc:
                out.unlink(missing_ok=True)
                if attempt == 3:
                    stats["failed"] += 1
                    print(f"FAIL [{folder}] {text[:40]!r}: {exc}")
                    return
                await asyncio.sleep(1.5 * (attempt + 1))


async def main():
    force = "--force" in sys.argv
    only = None
    if "--only" in sys.argv:
        only = sys.argv[sys.argv.index("--only") + 1]
    jobs = []
    unknown = Counter()
    voiced = 0
    for asset in sorted(DIALOGUE_DIR.glob("*.asset")):
        for item in load_asset_lines(asset):
            speaker = (item.get("speaker") or "").strip()
            text = (item.get("text") or "").strip()
            if not text:
                continue
            if not speaker:
                speaker = "Dẫn chuyện"  # empty speaker in .asset files = narration
            if speaker not in SPEAKERS:
                unknown[speaker] += 1
                continue
            voiced += 1
            jobs.append((speaker, text))
    for speaker, text in EXTRA_LINES:
        if speaker not in SPEAKERS:
            unknown[speaker] += 1
            continue
        voiced += 1
        jobs.append((speaker, text))

    unique = {}
    for speaker, text in jobs:
        unique[(speaker, text)] = None
    jobs = list(unique)
    if only:
        jobs = [(s, t) for s, t in jobs if SPEAKERS[s][0] == only]
        print(f"--only {only}: {len(jobs)} clips")

    if unknown:
        print("Speakers without voice (skipped):", dict(unknown))
    print(f"{voiced} voiced lines, {len(jobs)} unique clips")

    sem = asyncio.Semaphore(4)
    stats = {"made": 0, "skipped": 0, "failed": 0}
    tasks = [synth(sem, SPEAKERS[s][0], SPEAKERS[s][1], SPEAKERS[s][2], t, stats, force) for s, t in jobs]
    for i in range(0, len(tasks), 10):
        await asyncio.gather(*tasks[i : i + 10])
        print(f"  progress: {i + 10}/{len(tasks)}")
    print(f"done: {stats['made']} new, {stats['skipped']} already existed, {stats['failed']} failed")
    return 1 if stats["failed"] else 0


if __name__ == "__main__":
    sys.exit(asyncio.run(main()))
