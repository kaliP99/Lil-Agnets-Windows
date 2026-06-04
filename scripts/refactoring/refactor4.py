import re

with open("AgentChatForm.cs", "r", encoding="utf-8") as f:
    code = f.read()

# Aggressively remove methods that use old fields
methods_to_remove = [
    "LoadInitialMessage",
    "LoadChatHistory",
    "ClearChatHistory",
    "MicButton_Click",
    "VoiceTimer_Tick",
    "WaveOverlayPanel_Paint",
    "StopMicRecordingAndTranscribe",
    "SimulateSpeechTyping",
    "SlideIn",
    "SlideOut",
    "OnFormDragEnter",
    "OnFormDragLeave",
    "OnFormDragDrop",
    "ProcessBinaryFile",
    "DropZoneOverlay_Paint",
    "OnMessageKeyDown",
    "RegisterDragDrop"
]

def remove_method_body(code, method_name):
    pattern = r'(private (?:async )?void ' + method_name + r'\(.*?\)\s*\{)'
    match = re.search(pattern, code)
    if not match: return code
    start_idx = match.end()
    brace_count = 1
    end_idx = start_idx
    for i in range(start_idx, len(code)):
        if code[i] == '{': brace_count += 1
        elif code[i] == '}':
            brace_count -= 1
            if brace_count == 0:
                end_idx = i
                break
    return code[:start_idx] + "\n            // Removed for WebView2\n        " + code[end_idx:]

for m in methods_to_remove:
    code = remove_method_body(code, m)

# Also fix the `SendCurrentMessageAsync` to use WebView2 instead of _messageBox
code = re.sub(r'private async Task SendCurrentMessageAsync\(\)\s*\{.*?\}', '''private async Task SendCurrentMessageAsync()
        {
            // Sending logic handled in WebView2
        }''', code, flags=re.DOTALL)

with open("AgentChatForm.cs", "w", encoding="utf-8") as f:
    f.write(code)

