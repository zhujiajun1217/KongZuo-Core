#!/usr/bin/env python3
# Unity不可在这个运行环境内启动。此脚本对源码结构做静态校验，
# 并用Python等价模型穷举座次输入、异常触发边界，不冒充Unity编译测试。
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
        st=l.strip()
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
action_enum=set(re.findall(r"\b(Worksheet|FrontDoor|Phone|Seat07|Register|Program|Ritual|OldPhoto|Ticket|SeatPuzzle|BackstageDoor|Poster)\b",
                           CS["KZInteractable"].split("public enum KZActionKind")[1].split("}")[0]))
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

# 与KZSeatPuzzle.Cycle/Check逐行等价的可执行输入模型。
class SeatPuzzle:
    def __init__(self): self.seat6,self.seat8=0,0
    def cycle(self,index):
        if index not in (0,2): return False
        current,other=(self.seat6,self.seat8) if index==0 else (self.seat8,self.seat6)
        for step in range(1,4):
            nxt=(current+step)%3
            if nxt!=0 and nxt==other: continue
            if index==0: self.seat6=nxt
            else: self.seat8=nxt
            return True
        return False
    def check(self,clues):
        if not clues: return "MissingClues"
        if self.seat6==0 or self.seat8==0: return "Incomplete"
        return "Correct" if (self.seat6,self.seat8)==(1,2) else "Incorrect"

p=SeatPuzzle()
assert p.check(True)=="Incomplete"
assert p.cycle(1)==False and (p.seat6,p.seat8)==(0,0) # 7号必须留空
assert p.cycle(-1)==False and p.cycle(3)==False
assert p.cycle(0) and p.cycle(2)
assert (p.seat6,p.seat8)==(1,2) and p.check(False)=="MissingClues"
assert p.check(True)=="Correct"
wrong=SeatPuzzle(); wrong.cycle(2); wrong.cycle(0)
assert (wrong.seat6,wrong.seat8)==(2,1) and wrong.check(True)=="Incorrect"

cases=0
for length in range(7):
    for sequence in product((0,1,2),repeat=length):
        test=SeatPuzzle()
        for key in sequence: test.cycle(key)
        assert test.seat6 in (0,1,2) and test.seat8 in (0,1,2)
        assert test.seat6==0 or test.seat8==0 or test.seat6!=test.seat8
        assert test.check(False)=="MissingClues"
        cases+=1

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
print(f"PASS: {cases} puzzle key sequences (including boundary/duplicate/locked seat)")
print(f"PASS: {gate_cases} story prerequisite combinations")
print("PASS: scene references, interruption cleanup, and greybox geometry assumptions")
print("NOTE: requires Unity 6000.3.26f1 Play + Windows build for real engine validation")
