"""Trailer v2: the director's cut. Run from the project root (needs ffmpeg):

    python "Assets/Trailer/Tools~/assemble_trailer_v2.py"

Frames from TrailerRecorder (Trailer_Output/frames/<clip>), the game's own voice lines (Assets/Resources/Audio/Voice), its SFX
(Assets/Audio/Sfx, placed at the cue times TrailerShots logged in Trailer_Output/cues.txt) and its two generated music tracks.
Full 16:9 (no letterbox, so nobody loses their head or feet). Output: Trailer_Output/trailer_draft_v5.mp4.
"""
import os
import subprocess
import sys

ROOT = os.getcwd()
OUT = os.path.join(ROOT, "Trailer_Output")
FRAMES = os.path.join(OUT, "frames")
WORK = os.path.join(OUT, "segments_v5")
FPS = 30
VOICE = os.path.join(ROOT, "Assets", "Resources", "Audio", "Voice")
SFX = os.path.join(ROOT, "Assets", "Audio", "Sfx")
MUSIC = os.path.join(ROOT, "Assets", "Audio", "Music")
FONT = "C\\:/Windows/Fonts/times.ttf"
FONT_BOLD = "C\\:/Windows/Fonts/timesbd.ttf"

# transition INTO a segment: (xfade transition, seconds); "cut" = a hard cut
CUT = ("fade", 0.04)
DISSOLVE = ("fade", 0.4)
WHIP = ("smoothleft", 0.22)
WHIP_R = ("smoothright", 0.22)
WHITE = ("fadewhite", 0.3)
BLACK = ("fadeblack", 0.5)

# (key, clip or None for a card, start s in the clip, length s, transition in, caption, caption style)
SEGMENTS = [
    # 1. the hook: thunder in the dark, the narrator, the palace at sunset
    ("black0", None, 0, 1.2, CUT, None, None),
    ("crane", "v2_crane2", 0.0, 5.0, BLACK, None, None),
    ("drone", "v2_drone", 0.2, 3.8, DISSOLVE, None, None),
    # 2. the legend: Mị Nương, the king's decree
    ("minuong", "opening", 18.6, 2.6, DISSOLVE, None, None),
    ("king", "opening", 38.6, 2.8, CUT, None, None),
    # 3. the suitors
    ("sonhero", "v5_hero_son", 0.1, 3.0, WHIP, "SƠN TINH", "name"),
    ("thuyhero", "v5_hero_thuy", 0.1, 3.0, WHIP, "THỦY TINH", "name"),
    # 4. title drop
    ("title", None, 0, 3.0, CUT, "SƠN TINH – THỦY TINH", "title"),
    # 5. the six gift beasts: the king names the three of the mountain on their pictures, the three of the sea get their cards
    ("elephant", "v3_elephant", 0.3, 1.6, CUT, "Voi Chín Ngà", "beast"),
    ("rooster", "v3_rooster", 0.3, 1.2, CUT, "Gà Chín Cựa", "beast"),
    ("horse", "v3_horse2", 0.3, 1.8, CUT, "Ngựa Chín Hồng Mao", "beast"),
    ("croc", "v3_croc", 0.3, 1.8, WHIP_R, "Sấu Chín Đuôi", "beast"),
    ("shark", "v3_shark", 0.3, 1.8, CUT, "Mập Chín Vây", "beast"),
    ("turtle", "v3_turtle2", 0.3, 1.8, CUT, "Rùa Chín Chân", "beast"),
    # 6. the two lands: Sơn Tinh's mountain, then Thủy Tinh's sea
    ("songate", "v4_son_12", 0.3, 3.0, WHIP, None, None),
    ("stable", "stable", 0.0, 1.5, DISSOLVE, None, None),
    ("sonarena", "v4_son_4", 0.5, 2.8, DISSOLVE, None, None),
    ("boats", "boats", 0.0, 3.3, WHIP_R, None, None),
    ("karst", "v4_thuy_2", 0.3, 3.0, DISSOLVE, None, None),
    ("lighthouse", "lighthouse", 0.0, 3.3, DISSOLVE, None, None),
    # 7. the one fight: the rock wall against the wave, in profile (side, higher side in slow motion, side)
    ("clash1", "v4_clash_side", 0.4, 1.8, CUT, None, None),
    ("clash2", "v4_clash_high", 2.0, 2.8, CUT, None, None),
    ("clash3", "v4_clash_side", 4.8, 3.0, CUT, None, None),
    # 8. the end
    ("question", None, 0, 2.8, WHITE, "Còn người, sẽ bước vào vai ai?", "question"),
    ("endtitle", None, 0, 3.0, CUT, "SƠN TINH – THỦY TINH", "endtitle"),
    ("cta", None, 0, 3.2, DISSOLVE, None, "cta"),
]

# voice: (segment key, offset in it, file under Voice, trim to seconds or None, gain)
VOICES = [
    ("crane", 0.3, "Narrator/9c2ebe6e2fdf7c65.mp3", None, 1.0),          # Đời Hùng Vương thứ mười tám, nước Văn Lang thái bình, lúa tốt đầy đồng.
    ("minuong", 0.1, "HungVuong/fe942980d1677b17.mp3", None, 1.0),       # Truyền lệnh khắp bốn phương: ai có tài lạ hơn người...
    ("sonhero", 0.25, "SonTinh/2be61573d88a3854.mp3", 2.9, 1.1),         # Ta là Sơn Tinh, chúa miền non cao Tản Viên.
    ("thuyhero", 0.0, "ThuyTinh/0f9d82148333d191.mp3", 2.85, 1.1),       # Ta là Thủy Tinh, chúa miền biển cả.
    ("elephant", 0.0, "HungVuong/a71aace09a784dfd.mp3", 3.95, 1.0),      # Sính lễ gồm voi chín ngà, gà chín cựa, ngựa chín hồng mao.
    # NEW line (not in the game): edge-tts, same voice/rate as Hùng Vương's lines (vi-VN-NamMinhNeural, +8%), -3.6 dB to match them;
    # each name cut out on its own and laid on its animal
    ("croc", 0.15, os.path.join(OUT, "king_sea.mp3"), 1.05, 0.66, 0.15),     # Sấu chín đuôi,
    ("shark", 0.15, os.path.join(OUT, "king_sea.mp3"), 2.0, 0.66, 1.15),     # mập chín vây,
    ("turtle", 0.15, os.path.join(OUT, "king_sea.mp3"), 3.2, 0.66, 2.1),     # rùa chín chân.
    ("lighthouse", 0.0, "HungVuong/0bea2477236025de.mp3", None, 1.0),    # Được! Trời đất chứng giám. Bắt đầu!
    ("clash1", 0.1, "SonTinh/78d08ca2fb545c3a.mp3", None, 1.1),          # Ngươi dâng nước cao một thước, ta dựng núi cao một trượng. Đừng mong qua được non cao!
    ("question", 0.2, "Narrator/99499b5d596e557a.mp3", None, 1.1),       # Còn người, sẽ bước vào vai ai?
]

CROP_RIGHT = set()
# punch-in on the wide side take so the two fighters are bigger (crop 1/1.4 of the frame, a little below centre)
ZOOM = {"clash1", "clash3"}

# extra SFX not logged by the shots: (segment key, offset, file, gain)
EXTRA_SFX = [
    ("black0", 0.1, "Thunder_0.wav", 0.18),           # distant thunder only (about -15 dB), gone before the narrator
    ("sonhero", 0.0, "HitHeavy_0.wav", 0.3),          # low boom as the hero appears
    ("thuyhero", 0.0, "Splash_0.wav", 0.25),
    ("title", 1.1, "HitHeavy_0.wav", 0.6),            # the title slam (after a beat of silence), no thunder/rain
    ("cta", 0.0, "HitHeavy_0.wav", 0.45),             # the last drum hit
]


def run(args):
    subprocess.run(args, check=True)


def esc_path(p):
    return p.replace("\\", "/").replace(":", "\\:")


def text_file(name, text):
    path = os.path.join(WORK, name)
    with open(path, "w", encoding="utf-8") as f:
        f.write(text)
    return esc_path(path)


def alpha(length, fade_in=0.3, fade_out=0.3, start=0.15):
    t1 = length - 0.1
    return (f"alpha='if(lt(t,{start}),0,if(lt(t,{start + fade_in}),(t-{start})/{fade_in},"
            f"if(lt(t,{t1 - fade_out}),1,if(lt(t,{t1}),({t1}-t)/{fade_out},0))))'")


def caption_filters(key, text, style, length):
    gold = "fontcolor=0xF2C46A"
    shadow = "shadowcolor=black@0.85:shadowx=3:shadowy=3"
    if style == "name":      # small name tag, low left, never over the face
        p = text_file(f"{key}.txt", text)
        return [f"drawtext=fontfile='{FONT_BOLD}':textfile='{p}':{gold}:{shadow}:{alpha(length)}:fontsize=64:x=110:y=h-190"]
    if style == "beast":     # the beast's name, low left, gold
        p = text_file(f"{key}.txt", text)
        return [f"drawtext=fontfile='{FONT_BOLD}':textfile='{p}':{gold}:{shadow}:{alpha(length, 0.2, 0.2, 0.1)}:fontsize=60:x=110:y=h-180"]
    if style == "title":     # the slam: appears after a beat of black, scales in by fading fast
        p = text_file(f"{key}.txt", text)
        return [f"drawtext=fontfile='{FONT_BOLD}':textfile='{p}':{gold}:{alpha(length, 0.08, 0.3, 1.1)}:fontsize=120:x=(w-text_w)/2:y=(h-text_h)/2"]
    if style == "question":
        p = text_file(f"{key}.txt", text)
        return [f"drawtext=fontfile='{FONT}':textfile='{p}':fontcolor=white:{alpha(length)}:fontsize=72:x=(w-text_w)/2:y=(h-text_h)/2"]
    if style == "endtitle":
        p = text_file(f"{key}.txt", text)
        s = text_file(f"{key}_sub.txt", "PRU213")
        return [f"drawtext=fontfile='{FONT_BOLD}':textfile='{p}':{gold}:{alpha(length)}:fontsize=120:x=(w-text_w)/2:y=(h-text_h)/2-50",
                f"drawtext=fontfile='{FONT}':textfile='{s}':fontcolor=white:{alpha(length)}:fontsize=50:x=(w-text_w)/2:y=(h-text_h)/2+85"]
    if style == "cta":
        a = text_file(f"{key}_a.txt", "CHƠI NGAY")
        b = text_file(f"{key}_b.txt", "PC  •  2026")
        c = text_file(f"{key}_c.txt", "Nhóm PRU213 – FPT University")
        return [f"drawtext=fontfile='{FONT_BOLD}':textfile='{a}':{gold}:{alpha(length)}:fontsize=96:x=(w-text_w)/2:y=(h-text_h)/2-90",
                f"drawtext=fontfile='{FONT}':textfile='{b}':fontcolor=white:{alpha(length)}:fontsize=54:x=(w-text_w)/2:y=(h-text_h)/2+20",
                f"drawtext=fontfile='{FONT}':textfile='{c}':fontcolor=white@0.85:{alpha(length)}:fontsize=40:x=(w-text_w)/2:y=(h-text_h)/2+100"]
    return []


def build_segment(i, seg):
    key, clip, start, length, _, text, style = seg
    out = os.path.join(WORK, f"seg{i:02d}_{key}.mp4")
    if clip is None:
        src = ["-f", "lavfi", "-t", f"{length}", "-i", f"color=c=black:s=1920x1080:r={FPS}"]
    else:
        folder = os.path.join(FRAMES, clip)
        count = len([n for n in os.listdir(folder) if n.endswith(".jpg")])
        first, frames = int(round(start * FPS)), int(round(length * FPS))
        if first + frames > count:
            sys.exit(f"{clip}: needs frames {first}..{first + frames}, has {count}")
        src = ["-framerate", str(FPS), "-start_number", str(first), "-i", os.path.join(folder, "%05d.jpg"), "-frames:v", str(frames)]
    # a gentle warm grade on every game shot so the maps match
    vf = ["scale=1920:1080", "setsar=1"]
    if key in ZOOM:
        vf = ["crop=1372:772:274:200", "scale=1920:1080:flags=lanczos", "setsar=1"]
    if key in CROP_RIGHT:   # reframe in the edit: drop the left edge (the bronze drum) by cropping to the right 80 %
        vf = ["crop=1536:864:384:108", "scale=1920:1080:flags=lanczos", "setsar=1"]
    if clip is not None:
        vf.append("eq=saturation=1.08:contrast=1.04,colorbalance=rm=0.03:bm=-0.03:rh=0.02:bh=-0.02")
    vf += caption_filters(key, text, style, length)
    run(["ffmpeg", "-v", "error", "-y", *src, "-vf", ",".join(vf), "-r", str(FPS), "-c:v", "libx264", "-preset", "medium", "-crf", "16", "-pix_fmt", "yuv420p", out])
    return out


def main():
    os.makedirs(WORK, exist_ok=True)
    paths = [build_segment(i, s) for i, s in enumerate(SEGMENTS)]

    # timeline: start time of each segment after the transitions eat into the previous one
    starts, t = [], 0.0
    for i, s in enumerate(SEGMENTS):
        if i > 0:
            t -= s[4][1]
        starts.append(t)
        t += s[3]
    total = t
    start_of = {s[0]: starts[i] for i, s in enumerate(SEGMENTS)}
    seg_of = {s[0]: s for s in SEGMENTS}

    inputs, chain = [], []
    for p in paths:
        inputs += ["-i", p]
    last = "[0:v]"
    for k in range(1, len(SEGMENTS)):
        name, dur = SEGMENTS[k][4]
        label = f"[v{k}]"
        chain.append(f"{last}[{k}:v]xfade=transition={name}:duration={dur}:offset={starts[k]:.3f}{label}")
        last = label
    chain.append(f"{last}fade=t=out:st={total - 0.6:.3f}:d=0.6[vout]")

    # ---- audio
    n = len(paths)
    audio_inputs = []

    def add_input(path, loop=False):
        nonlocal n
        audio_inputs.extend((["-stream_loop", "-1"] if loop else []) + ["-i", path])
        n += 1
        return n - 1

    # voice
    vlabels = []
    for j, v in enumerate(VOICES):
        key, off, f, trim, gain = v[:5]
        st = v[5] if len(v) > 5 else 0.0
        idx = add_input(f if os.path.isabs(f) else os.path.join(VOICE, f))
        at = start_of[key] + off
        tr = f"atrim={st}:{trim},asetpts=PTS-STARTPTS," if trim else ""
        chain.append(f"[{idx}:a]{tr}afade=t=out:st={(trim - st if trim else 99) - 0.12}:d=0.12,aformat=sample_rates=44100:channel_layouts=stereo,volume={gain},adelay={int(at * 1000)}|{int(at * 1000)}[vo{j}]")
        vlabels.append(f"[vo{j}]")
    chain.append(f"{''.join(vlabels)}amix=inputs={len(vlabels)}:duration=longest:normalize=0,apad=whole_dur={total:.3f},asplit=2[voice][vkey]")

    # SFX: logged cues inside the used part of each clip, plus the extras
    cues = []
    cue_file = os.path.join(OUT, "cues.txt")
    if os.path.exists(cue_file):
        for line in open(cue_file, encoding="utf-8"):
            parts = line.strip().split("\t")
            if len(parts) == 3:
                cues.append((parts[0], float(parts[1]), parts[2]))
    placed = []
    for s in SEGMENTS:
        key, clip, cstart, length = s[0], s[1], s[2], s[3]
        if clip is None:
            continue
        seen = set()
        for c_clip, c_t, c_sfx in cues:
            if c_clip == clip and cstart - 0.05 <= c_t < cstart + length and (c_t, c_sfx) not in seen:
                seen.add((c_t, c_sfx))
                if "Thunder" in c_sfx:   # no rain/thunder bed in the edit
                    continue
                quiet = key in ("elephant", "rooster", "horse", "croc", "shark", "turtle")
                placed.append((start_of[key] + (c_t - cstart), c_sfx, 0.22 if quiet else 0.32))
    for key, off, f, gain in EXTRA_SFX:
        placed.append((start_of[key] + off, f, gain))
    slabels = []
    for j, (at, f, gain) in enumerate(placed):
        path = os.path.join(SFX, f if f.endswith(".wav") else f + ".wav")
        if not os.path.exists(path):
            print("missing sfx", path)
            continue
        idx = add_input(path)
        chain.append(f"[{idx}:a]aformat=sample_rates=44100:channel_layouts=stereo,volume={gain},adelay={int(max(at, 0) * 1000)}|{int(max(at, 0) * 1000)}[sx{j}]")
        slabels.append(f"[sx{j}]")
    chain.append(f"{''.join(slabels)}amix=inputs={len(slabels)}:duration=longest:normalize=0,apad=whole_dur={total:.3f}[sfx]")

    # music: quiet cutscene track until the title drop (silent for the beat before the slam), the battle track from the slam to the end
    drop = start_of["title"] + 1.1
    a = add_input(os.path.join(MUSIC, "MusicPalace.wav"), loop=True)
    b = add_input(os.path.join(MUSIC, "MusicBattle.wav"), loop=True)
    chain.append(f"[{a}:a]atrim=0:{drop - 1.1:.3f},aformat=sample_rates=44100:channel_layouts=stereo,afade=t=in:st=0.5:d=2,afade=t=out:st={drop - 1.6:.3f}:d=0.5,volume=0.55[ma]")
    chain.append(f"[{b}:a]atrim=0:{total - drop:.3f},aformat=sample_rates=44100:channel_layouts=stereo,afade=t=out:st={total - drop - 2.5:.3f}:d=2.5,volume=0.8,adelay={int(drop * 1000)}|{int(drop * 1000)}[mb]")
    chain.append(f"[ma][mb]amix=inputs=2:duration=longest:normalize=0,apad=whole_dur={total:.3f}[music]")
    # duck the music under the voices
    chain.append("[music][vkey]sidechaincompress=threshold=0.03:ratio=8:attack=20:release=350[mduck]")
    chain.append(f"[voice][sfx][mduck]amix=inputs=3:duration=longest:normalize=0,atrim=0:{total:.3f},alimiter=limit=0.9,loudnorm=I=-16:TP=-1.5:LRA=11,aresample=48000[aout]")

    out = os.path.join(OUT, "trailer_draft_v5.mp4")
    script = os.path.join(WORK, "filter.txt")
    with open(script, "w", encoding="utf-8") as f:
        f.write(";\n".join(chain))
    run(["ffmpeg", "-v", "error", "-y", *inputs, *audio_inputs, "-filter_complex_script", script, "-map", "[vout]", "-map", "[aout]",
         "-c:v", "libx264", "-preset", "slow", "-crf", "18", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart", out])
    print(f"done: {out} ({total:.1f} s)")
    with open(os.path.join(WORK, "timeline.txt"), "w", encoding="utf-8") as f:
        for i, s in enumerate(SEGMENTS):
            f.write(f"{starts[i]:6.2f}  {s[3]:4.1f}s  {s[0]:<12} {s[1] or '(card)'}\n")


if __name__ == "__main__":
    main()
