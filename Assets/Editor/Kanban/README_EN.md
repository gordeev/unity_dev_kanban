# Kanban Board for Unity Editor

A simple kanban board inside Unity Editor for project task management.

## Installation

### Option 1: Unity Package
1. In Unity, navigate to `Assets/Editor/Kanban`
2. Right-click → **Export Package...**
3. Make sure all files are selected (uncheck `kanban_board.json` for a clean package)
4. Save the `.unitypackage`

To import: **Assets → Import Package → Custom Package**

### Option 2: Copy
Copy the `Assets/Editor/Kanban` folder into your project.

## Usage

Open: **Tools → Kanban Board** (or `Ctrl+Shift+K` / `Cmd+Shift+K`)

### Columns
- 📋 **Backlog** — ideas, later
- 📝 **Todo** — planned  
- 🔨 **Doing** — in progress
- ✅ **Done** — completed

### Priorities
- 🔴 Critical (red)
- 🟠 High (orange)
- 🔵 Normal (blue)
- ⚪ Low (gray)

### Features
- **Drag & Drop** — move cards between columns
- **Subtasks** — checklist inside a task
- **History** — log of all task changes
- **Archive** — hide completed tasks
- **Search** — filter by title, notes, tags
- **Tags** — categorize tasks (comma-separated)
- **Links** — URL to issue/PR

### Hotkeys
| Key | Action |
|-----|--------|
| `Enter` | Add task (when in input field) |
| `Ctrl + ← →` | Move selected task |
| `Ctrl + D` | Duplicate task |
| `Ctrl + Delete` | Delete task |
| `Ctrl + Shift + A` | Archive all Done |
| `Escape` | Deselect |
| `Double Click` | Open task editor |

## Data Storage

Data is stored in `Assets/Editor/Kanban/kanban_board.json` — human-readable JSON.

### Team Collaboration
- The `.json` file can be committed to Git
- Conflicts can be merged as regular text
- Each task has a unique ID

### Recommendations for Teams
- Add `kanban_board.json` to `.gitignore` if everyone wants their own board
- Or commit it for a shared board (merge conflicts manually)

## Files

```
Assets/Editor/Kanban/
├── KanbanWindow.cs      # Main window
├── KanbanData.cs        # Data models and storage
├── kanban_board.json    # Board data (created automatically)
├── README.md            # Russian documentation
└── README_EN.md         # This file
```

## Requirements

- Unity 2021.3+ (uses C# 9 features)
- Works with any render pipeline (Built-in, URP, HDRP)

## License

MIT — use however you like.

