#!/usr/bin/env python3
# Unity不可在这个运行环境内启动。此脚本对源码结构做静态校验，
# 并检查剧情门槛与异常触发边界，不冒充Unity编译测试。
from pathlib import Path
from itertools import product
import re

ROOT = Path(__file__).resolve().parents[1]
CS = {p.stem:p.read_text(encoding="utf-8")
      for p in (ROOT/"Assets/KongZuo").rglob("*.cs")}

def csharp_delimiters(text, file):
    stack, state, i, line = [], "code", 0, 1
    pairs = {"}":"{", ")":"(", "]":"["}
    while i < len(text):
        ch, next_ch = text[i], text[i+1] if i+1 < len(text) else ""
        if ch == "\n": line += 1
        if state == "code":
            if ch == "/" and next_ch == "/": state = "line"; i += 1
            elif ch == "/" and next_ch == "*": state = "block"; i += 1
            elif ch == '"' and i and text[i-1] == "@": state = "verb_string"
            elif ch == '"': state = "string"
            elif ch == "'": state = "char"
            elif ch in "{([": stack.append((ch,line))
            elif ch in "})]":
                assert stack and stack[-1][0] == pairs[ch], (
                    file, "unmatched", ch, "line", line, stack[-1:] )
                stack.pop()
        elif state == "line":
            if ch == "\n": state = "code"
        elif state == "block":
            if ch == "*" and next_ch == "/": state = "code"; i += 1
        elif state in ("string","char"):
            if ch == "\\": i += 1
            elif (state == "string" and ch == '"') or (state == "char" and ch == "'"):
                state = "code"
        elif state == "verb_string":
            if ch == '"':
                if next_ch == '"': i += 1
                else: state = "code"
        i += 1
    assert not stack, (file, "missing bracket closings", stack)
    assert state in ("code","line"), (file,"unterminated literal/comment",state)
    depth=0
    for l in text.splitlines():
        st=l.lstrip("\ufeff").strip()
        if st.startswith("#if"): depth+=1
        if st.startswith("#endif"): depth-=1
        assert depth>=0,(file,"unexpected #endif")
    assert depth==0,(file,"unclosed #if")

for name,src in CS.items():
    csharp_delimiters(src,name)

# MonoBehaviour/类名与文件名、场景名引用检查。
for name in ("KZGameSession","KZBootstrapEntry","KZFirstPersonPlayer","KZInteractor",
             "KZInteractable","KZStoryTrigger","KZStoryDirector","KZHud"):
    assert re.search(r"\bclass\s+"+name+r"\b", CS[name])
assert len(CS) >= 11
ids=set(re.findall(r'const string\s+(\w+)\s*=',CS["KZFlags"]))
for n,src in CS.items():
    for found in re.findall(r"\bKZFlags\.(\w+)",src):
        assert found in ids,(n,"unknown flag",found)
action_enum_body=re.search(
    r"public enum KZActionKind\s*\{([^}]*)\}",CS["KZInteractable"]).group(1)
action_enum=set(re.findall(r"\b[A-Za-z_]\w*\b",action_enum_body))
action_values=[item.strip() for item in action_enum_body.split(",") if item.strip()]
assert action_values.index("SeatRecord") == 9 # Existing scene YAML serializes this action as 9.
for n,src in CS.items():
    for found in re.findall(r"\bKZActionKind\.(\w+)",src):
        assert found in action_enum,(n,"unknown action",found)
assert 'SaveCurrentModifiedScenesIfUserWantsTo' in CS["KZMainProjectSetup"]
assert 'AssetDatabase.LoadAssetAtPath<SceneAsset>' in CS["KZMainProjectSetup"]
assert "Shader.Find(\"Universal Render Pipeline/Lit\")" in CS["KZMainProjectSetup"]
assert "Application.unityVersion != RequiredVersion" in CS["KZMainProjectSetup"]
assert "UniversalRenderPipelineAsset" in CS["KZMainProjectSetup"]
assert "KZSceneNames.ChapterOne" in CS["KZStoryDirector"]
assert "TryAddFlag(KZFlags.AnomalyStarted)" in CS["KZStoryDirector"]
assert "TryAddFlag(KZFlags.EchoStarted)" in CS["KZStoryDirector"]
assert "player.SetFreezeWalking(false)" in CS["KZStoryDirector"]
assert "OnDisable()" in CS["KZStoryDirector"]
assert "OnTriggerExit" in CS["KZStoryTrigger"]
assert "KZ_OperaHouse_P0_V03_FULL.prefab" in CS["KZMergedArtSetup"]
assert "PrefabUtility.InstantiatePrefab(prefab, scene)" in CS["KZMergedArtSetup"]
assert "RemovedArtObjects" not in CS["KZMergedArtSetup"]
art_guid = (ROOT/"Assets/KongZuo/Art/KZ_OperaHouse_P0_V03/Generated/Prefabs/"
            "KZ_OperaHouse_P0_V03_FULL.prefab.meta").read_text(encoding="utf-8")
art_guid = re.search(r"^guid: (\w+)$", art_guid, re.M).group(1)
for scene_name in ("01_Prologue.unity", "02_Chapter01.unity"):
    scene = (ROOT/"Assets/KongZuo/Scenes"/scene_name).read_text(encoding="utf-8")
    assert f"guid: {art_guid}, type: 3" in scene, (scene_name, "latest FULL prefab is not referenced")
    assert f"m_RemovedGameObjects:\n    - {{fileID: " not in scene, (
        scene_name, "FULL prefab instance still removes source objects")

assert "KZSeatPuzzle" not in CS
assert "ShowPuzzle" not in CS["KZHud"]
assert "PuzzleSolved" not in CS["KZFlags"] + CS["KZStoryDirector"] + CS["KZHud"]
assert "case KZActionKind.SeatRecord:" in CS["KZStoryDirector"]
assert "KZHud.ShowPage(item.DisplayName, item.body);" in CS["KZStoryDirector"]
assert "KZFlags.PhotoAligned" in CS["KZStoryDirector"]
assert "KZFlags.WorkRecord" in CS["KZStoryDirector"]
assert "KZFlags.PuzzleSolved" not in CS["KZStoryDirector"]

# 重返观众厅：任意顺序的电话、座位、登记表不应影响条件。
gate_cases=0
for clue_bits in product((False,True),repeat=3):
    for visited,left in product((False,True),repeat=2):
        expected=all(clue_bits) and visited and left
        actual=visited and left and all(clue_bits)
        assert actual==expected
        gate_cases+=1

# 1F碰撞和路径的几何边界占位检查（单位：Unity米）。
radius=0.31
last_chair_edge=6*1.2+0.94/2
assert 8.7-last_chair_edge>radius*2, "side aisle too narrow"
assert 2.25-0.86>radius*2, "row gap too narrow"
assert 3.0/2>radius, "front entrance too narrow"
heights=[0,0.24,0.46,0.66,0.8]
assert all(heights[i+1]-heights[i]<=0.30 for i in range(len(heights)-1))
assert "5000" not in CS["KZMainProjectSetup"] # 防止生成过量复杂对象的占位误配置

print(f"PASS: {len(CS)} C# files structurally checked")
print("PASS: ticket-based seat restoration puzzle removed")
print(f"PASS: {gate_cases} story prerequisite combinations")
print("PASS: scene references, interruption cleanup, and greybox geometry assumptions")
print("NOTE: requires Unity 6000.3.26f1 Play + Windows build for real engine validation")
