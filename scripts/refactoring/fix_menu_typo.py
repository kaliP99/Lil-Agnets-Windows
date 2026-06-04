import re

with open("AgentChatForm.cs", "r", encoding="utf-8") as f:
    code = f.read()

code = code.replace("__modelMenu", "_modelMenu")

with open("AgentChatForm.cs", "w", encoding="utf-8") as f:
    f.write(code)
