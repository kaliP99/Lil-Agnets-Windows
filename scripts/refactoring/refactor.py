import re
import sys

def remove_method_body(code, method_name):
    # Find method declaration
    pattern = r'(private (?:async )?void ' + method_name + r'\(.*?\)\s*\{)'
    match = re.search(pattern, code)
    if not match:
        return code
    
    start_idx = match.end()
    
    # Count braces to find the end
    brace_count = 1
    end_idx = start_idx
    for i in range(start_idx, len(code)):
        if code[i] == '{':
            brace_count += 1
        elif code[i] == '}':
            brace_count -= 1
            if brace_count == 0:
                end_idx = i
                break
                
    new_code = code[:start_idx] + "\n            // Removed for WebView2\n        " + code[end_idx:]
    return new_code

def main():
    with open("AgentChatForm.cs", "r", encoding="utf-8") as f:
        code = f.read()

    # Add WebView2 using statement
    code = code.replace("using System.Windows.Forms;", "using System.Windows.Forms;\nusing Microsoft.Web.WebView2.Core;\nusing Microsoft.Web.WebView2.WinForms;")

    # Replace fields
    code = re.sub(r'private FlowLayoutPanel _chatPanel.*?(?=private Panel _waveOverlayPanel)', 'private Microsoft.Web.WebView2.WinForms.WebView2 _webView = null!;\n        ', code, flags=re.DOTALL)

    # Empty out methods
    methods_to_empty = [
        "PositionInputControls",
        "AdjustInputHeight",
        "MessageBox_TextChanged",
        "ScrollToBottomSmooth",
        "ResizeMessageRows",
        "BuildSystemMessage",
        "HandleAgentAvatarClick"
    ]
    for m in methods_to_empty:
        code = remove_method_body(code, m)

    with open("AgentChatForm_WebView2.cs", "w", encoding="utf-8") as f:
        f.write(code)

if __name__ == "__main__":
    main()
