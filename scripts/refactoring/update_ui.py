import re

with open("chat_ui.html", "r", encoding="utf-8") as f:
    html = f.read()

# Change user message row alignment to match Gemini
html = html.replace('.message-row.user {\n            justify-content: center;\n        }', '.message-row.user {\n            justify-content: center;\n        }')
html = html.replace('.message-row.user .message-content {\n            justify-content: flex-end;\n        }', '.message-row.user .message-content {\n            justify-content: flex-start;\n        }')

# Also change the user bubble styling to be more Gemini-like (gray pill on the right wait no, they are left aligned!)
html = html.replace("""        .user .bubble {
            background-color: var(--bubble-user);
            padding: 12px 20px;
            border-radius: 20px;
        }""", """        .user .bubble {
            background-color: var(--bubble-user);
            padding: 12px 20px;
            border-radius: 20px;
            font-size: 15px;
        }""")

# Add user avatar support
html = html.replace("""function appendUserMessage(text) {
            const row = document.createElement('div');
            row.className = 'message-row user';
            row.innerHTML = `
                <div class="message-content">
                    <div class="bubble">${escapeHtml(text)}</div>
                </div>
            `;""", """function appendUserMessage(text) {
            const row = document.createElement('div');
            row.className = 'message-row user';
            row.innerHTML = `
                <div class="message-content">
                    <div class="avatar" style="background-color: #047857;">U</div>
                    <div class="bubble-container">
                        <div class="agent-name" style="font-weight: 600; font-size: 14px; margin-bottom: 4px; color: #9CA3AF;">You</div>
                        <div class="bubble">${escapeHtml(text)}</div>
                    </div>
                </div>
            `;""")

with open("chat_ui.html", "w", encoding="utf-8") as f:
    f.write(html)
