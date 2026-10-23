"""Assembles the draft trailer from the frames TrailerRecorder wrote (Trailer_Output/frames/<clip>/00000.jpg, 30 fps, 1920x1080).

    python Assets/Trailer/Tools~/assemble_trailer.py            (run from the project root; needs ffmpeg on PATH)

Each segment = a slice of one clip (or a black card), letterboxed to 2.39:1, with an optional caption; the segments are joined with
short crossfades, the music is the game's own generated tracks (Assets/Audio/Music). Output: Trailer_Output/trailer_draft_v1.mp4.
"""
import os
import subprocess
import sys

ROOT = os.getcwd()
OUT = os.path.join(ROOT, "Trailer_Output")
FRAMES = os.path.join(OUT, "frames")
WORK = os.path.join(OUT, "segments")
FPS = 30
FADE = 0.35                      # crossfade between segments
BAR = 138                        # letterbox bar height (1080 - 1920 / 2.39) / 2
FONT = "C\\:/Windows/Fonts/times.ttf"
FONT_BOLD = "C\\:/Windows/Fonts/timesbd.ttf"

# (clip or None for black, start s, length s, caption, caption style)
SEGMENTS = [
    ("palace_fly", 0.0, 6.0, "Thuở Vua Hùng thứ mười tám...", "story"),
    ("opening", 2.0, 3.0, None, None),
    ("opening", 18.5, 3.0, "Vua có người con gái tên Mị Nương", "story"),
    ("opening", 38.5, 3.0, None, None),
    ("opening", 44.0, 3.5, "Nhà vua kén rể khắp bốn phương", "story"),
    ("opening", 56.0, 3.5, "Sơn Tinh – Chúa núi", "name"),
    ("opening", 62.5, 5.5, "Thủy Tinh – Chúa nước", "name"),
    ("opening", 91.5, 3.2, "Voi chín ngà, gà chín cựa, ngựa chín hồng mao", "story"),
    ("stable", 0.0, 2.2, "Thu thập sính lễ", "chapter"),
    ("ninja_fight", 1.0, 5.0, None, None),
    ("boats", 0.0, 3.5, None, None),
    ("lighthouse", 0.0, 3.3, None, None),
    ("croc_fight", 1.2, 3.8, "Đánh bại quái thú", "chapter"),
    ("skill_son_0", 0.8, 1.5, None, None),
    ("skill_thuy_0", 0.6, 1.5, None, None),
    ("skill_son_1", 0.6, 1.5, None, None),
    ("skill_thuy_1", 0.5, 1.5, None, None),
    ("skill_son_2", 0.5, 1.5, None, None),
    ("skill_thuy_2", 1.0, 1.5, None, None),
    ("skill_son_combo", 0.4, 1.4, None, None),
    ("skill_thuy_combo", 0.6, 1.4, None, None),
    ("clash", 0.3, 8.0, None, None),
    (None, 0.0, 3.2, "Ai sẽ cưới được Mị Nương?", "question"),
    (None, 0.0, 6.5, "SƠN TINH – THỦY TINH", "title"),
]

MUSIC_A = os.path.join(ROOT, "Assets", "Audio", "Music", "MusicCutscene.wav")
MUSIC_B = os.path.join(ROOT, "Assets", "Audio", "Music", "MusicBattle.wav")


def run(args):
    print(" ".join(args)[:200])
    subprocess.run(args, check=True)


def caption_filters(i, text, style, length):
    """drawtext filters for one caption (text read from a UTF-8 file, so no escaping of ':' or ',')."""
    if not text:
        return []
    path = os.path.join(WORK, f"cap{i:02d}.txt").replace("\\", "/").replace(":", "\\:")
    with open(os.path.join(WORK, f"cap{i:02d}.txt"), "w", encoding="utf-8") as f:
        f.write(text)
    t0, t1 = 0.3, length - 0.3
    alpha = f"alpha='if(lt(t,{t0}),0,if(lt(t,{t0 + 0.4}),(t-{t0})/0.4,if(lt(t,{t1 - 0.4}),1,if(lt(t,{t1}),({t1}-t)/0.4,0))))'"
    common = f"textfile='{path}':fontcolor=white:shadowcolor=black@0.8:shadowx=2:shadowy=2:{alpha}"
    if style == "story":      # under the picture, in the lower bar
        return [f"drawtext=fontfile='{FONT}':{common}:fontsize=40:x=(w-text_w)/2:y=h-{BAR}+(({BAR}-text_h)/2)"]
    if style == "name":       # big, lower third of the picture
        return [f"drawtext=fontfile='{FONT_BOLD}':{common}:fontsize=72:x=(w-text_w)/2:y=h-{BAR}-150"]
    if style == "chapter":    # centre
        return [f"drawtext=fontfile='{FONT_BOLD}':{common}:fontsize=80:x=(w-text_w)/2:y=(h-text_h)/2"]
    if style == "question":
        return [f"drawtext=fontfile='{FONT}':{common}:fontsize=70:x=(w-text_w)/2:y=(h-text_h)/2"]
    if style == "title":
        sub = os.path.join(WORK, f"cap{i:02d}b.txt")
        with open(sub, "w", encoding="utf-8") as f:
            f.write("PRU213")
        sub = sub.replace("\\", "/").replace(":", "\\:")
        gold = "fontcolor=0xF2C46A"
        return [f"drawtext=fontfile='{FONT_BOLD}':textfile='{path}':{gold}:{alpha}:fontsize=110:x=(w-text_w)/2:y=(h-text_h)/2-50",
                f"drawtext=fontfile='{FONT}':textfile='{sub}':fontcolor=white:{alpha}:fontsize=46:x=(w-text_w)/2:y=(h-text_h)/2+80"]
    return []


def build_segment(i, clip, start, length, text, style):
    out = os.path.join(WORK, f"seg{i:02d}.mp4")
    if clip is None:
        src = ["-f", "lavfi", "-t", f"{length}", "-i", f"color=c=black:s=1920x1080:r={FPS}"]
    else:
        folder = os.path.join(FRAMES, clip)
        count = len([n for n in os.listdir(folder) if n.endswith(".jpg")])
        first = int(round(start * FPS))
        frames = int(round(length * FPS))
        if first + frames > count:
            sys.exit(f"{clip}: needs frames {first}..{first + frames}, has {count}")
        src = ["-framerate", str(FPS), "-start_number", str(first), "-i", os.path.join(folder, "%05d.jpg"), "-frames:v", str(frames)]
    vf = ["scale=1920:1080", "setsar=1", f"drawbox=x=0:y=0:w=iw:h={BAR}:color=black:t=fill", f"drawbox=x=0:y=ih-{BAR}:w=iw:h={BAR}:color=black:t=fill"]
    vf += caption_filters(i, text, style, length)
    run(["ffmpeg", "-v", "error", "-y", *src, "-vf", ",".join(vf), "-r", str(FPS), "-c:v", "libx264", "-preset", "medium", "-crf", "16", "-pix_fmt", "yuv420p", out])
    return out, length


def main():
    os.makedirs(WORK, exist_ok=True)
    segs = [build_segment(i, *s) for i, s in enumerate(SEGMENTS)]

    # crossfade chain
    inputs, chain, offset = [], [], 0.0
    for path, _ in segs:
        inputs += ["-i", path]
    last = "[0:v]"
    offset = segs[0][1]
    for k in range(1, len(segs)):
        offset -= FADE
        label = f"[v{k}]"
        chain.append(f"{last}[{k}:v]xfade=transition=fade:duration={FADE}:offset={offset:.3f}{label}")
        last = label
        offset += segs[k][1]
    total = offset
    # fade in from black at the start, out at the end
    chain.append(f"{last}fade=t=in:st=0:d=0.8,fade=t=out:st={total - 1.2:.3f}:d=1.2[vout]")

    # music: the cutscene track under the story, the battle track from the gift hunt on
    switch = sum(s[2] for s in SEGMENTS[:8]) - 7 * FADE
    a = len(segs)
    inputs += ["-stream_loop", "-1", "-i", MUSIC_A, "-stream_loop", "-1", "-i", MUSIC_B]
    chain.append(f"[{a}:a]atrim=0:{switch + 1.5:.3f},afade=t=in:st=0:d=1.5,afade=t=out:st={switch - 0.5:.3f}:d=2,volume=0.9[ma]")
    chain.append(f"[{a + 1}:a]atrim=0:{total - switch + 0.5:.3f},afade=t=in:st=0:d=1,afade=t=out:st={total - switch - 3:.3f}:d=3,adelay={int(switch * 1000)}|{int(switch * 1000)}[mb]")
    chain.append(f"[ma][mb]amix=inputs=2:duration=longest:normalize=0,atrim=0:{total:.3f}[aout]")

    out = os.path.join(OUT, "trailer_draft_v1.mp4")
    run(["ffmpeg", "-v", "error", "-y", *inputs, "-filter_complex", ";".join(chain), "-map", "[vout]", "-map", "[aout]",
         "-c:v", "libx264", "-preset", "slow", "-crf", "18", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart", out])
    print(f"done: {out} ({total:.1f} s)")


if __name__ == "__main__":
    main()
