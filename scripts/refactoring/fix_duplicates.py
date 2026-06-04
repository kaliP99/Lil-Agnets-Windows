import re

with open("AgentChatForm.cs", "r", encoding="utf-8") as f:
    lines = f.readlines()

def revert_injection(start_line):
    idx = start_line - 1
    # Check if this is the start of the injection
    if "WEBVIEW2 INJECTION" in lines[idx]:
        # Delete lines until PositionInputControls();
        end_idx = idx
        while end_idx < len(lines) and "PositionInputControls();" not in lines[end_idx]:
            end_idx += 1
        
        # Replace the whole block with just PositionInputControls();
        del lines[idx:end_idx+1]
        lines.insert(idx, "            PositionInputControls();\n")

# Revert from bottom up to avoid line shifting
revert_injection(2221)
revert_injection(1207)

with open("AgentChatForm.cs", "w", encoding="utf-8") as f:
    f.writelines(lines)
    
