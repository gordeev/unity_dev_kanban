using System.Linq;
using UnityEditor;
using UnityEngine;

public class KanbanWindow : EditorWindow
{
    private const string AssetPath = "Assets/Editor/Kanban/KanbanBoard.asset";
    private KanbanBoard board;

    private Vector2[] scroll = new Vector2[4];
    private string quickTitle = "";
    private string search = "";

    private KanbanTask draggingTask;
    private int dragSourceColumn = -1;
    private KanbanStatus? hoverColumn;

    // Colors for priorities
    private static readonly Color[] PriorityColors = {
        new Color(0.9f, 0.3f, 0.3f, 1f),  // 0 - Critical (red)
        new Color(0.95f, 0.6f, 0.2f, 1f), // 1 - High (orange)
        new Color(0.3f, 0.7f, 0.9f, 1f),  // 2 - Normal (blue)
        new Color(0.5f, 0.5f, 0.5f, 1f),  // 3 - Low (gray)
    };

    private static readonly Color[] ColumnColors = {
        new Color(0.35f, 0.35f, 0.4f, 0.3f),  // Backlog
        new Color(0.4f, 0.5f, 0.6f, 0.3f),    // Todo
        new Color(0.5f, 0.6f, 0.4f, 0.3f),    // Doing
        new Color(0.3f, 0.5f, 0.3f, 0.3f),    // Done
    };

    [MenuItem("Tools/Kanban Board")]
    public static void Open() => GetWindow<KanbanWindow>("Kanban");

    private void OnEnable()
    {
        LoadOrCreateBoard();
    }

    private void LoadOrCreateBoard()
    {
        board = AssetDatabase.LoadAssetAtPath<KanbanBoard>(AssetPath);
        if (board == null)
        {
            // Ensure directory exists
            var dir = System.IO.Path.GetDirectoryName(AssetPath);
            if (!AssetDatabase.IsValidFolder(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            board = CreateInstance<KanbanBoard>();
            AssetDatabase.CreateAsset(board, AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Kanban] Created new board at {AssetPath}");
        }
    }

    private void OnGUI()
    {
        if (board == null)
        {
            LoadOrCreateBoard();
            if (board == null)
            {
                EditorGUILayout.HelpBox("Failed to load or create Kanban board.", MessageType.Error);
                return;
            }
        }

        DrawTopBar();
        EditorGUILayout.Space(4);

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawColumn(KanbanStatus.Backlog, 0);
            DrawColumn(KanbanStatus.Todo, 1);
            DrawColumn(KanbanStatus.Doing, 2);
            DrawColumn(KanbanStatus.Done, 3);
        }

        HandleDragAndDrop();

        if (GUI.changed)
        {
            SaveBoard();
        }
    }

    private void SaveBoard()
    {
        if (board != null)
        {
            EditorUtility.SetDirty(board);
            AssetDatabase.SaveAssets();
        }
    }

    private void DrawTopBar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("Add:", GUILayout.Width(30));

            GUI.SetNextControlName("QuickAddField");
            quickTitle = GUILayout.TextField(quickTitle, EditorStyles.toolbarTextField, GUILayout.Width(200));

            // Handle Enter key to add task
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                if (GUI.GetNameOfFocusedControl() == "QuickAddField")
                {
                    AddQuickTask();
                    Event.current.Use();
                }
            }

            if (GUILayout.Button("+Backlog", EditorStyles.toolbarButton, GUILayout.Width(55)))
                AddQuickTask(KanbanStatus.Backlog);

            if (GUILayout.Button("+Todo", EditorStyles.toolbarButton, GUILayout.Width(45)))
                AddQuickTask(KanbanStatus.Todo);

            if (GUILayout.Button("+Doing", EditorStyles.toolbarButton, GUILayout.Width(50)))
                AddQuickTask(KanbanStatus.Doing);

            GUILayout.FlexibleSpace();

            GUILayout.Label("🔍", GUILayout.Width(18));
            search = GUILayout.TextField(search, EditorStyles.toolbarTextField, GUILayout.Width(150));

            if (!string.IsNullOrEmpty(search) && GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(20)))
            {
                search = "";
                GUI.FocusControl(null);
            }

            GUILayout.Space(4);
            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                SaveBoard();
                Debug.Log("[Kanban] Board saved.");
            }
        }
    }

    private void AddQuickTask(KanbanStatus status = KanbanStatus.Todo)
    {
        if (string.IsNullOrWhiteSpace(quickTitle))
        {
            // Show notification instead of doing nothing
            ShowNotification(new GUIContent("Enter a task title first"), 1f);
            return;
        }

        board.tasks.Add(new KanbanTask
        {
            title = quickTitle.Trim(),
            status = status
        });
        quickTitle = "";
        GUI.changed = true;
        GUI.FocusControl(null);
        Repaint();
    }

    private void DrawColumn(KanbanStatus status, int idx)
    {
        float columnWidth = position.width / 4f - 6;

        using (new EditorGUILayout.VerticalScope(GUILayout.Width(columnWidth)))
        {
            // Column header with count
            var tasks = board.tasks.Where(t => t.status == status).ToList();
            var filteredTasks = tasks.Where(MatchesSearch).ToList();
            var headerText = $"{GetStatusIcon(status)} {status} ({filteredTasks.Count})";

            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter
            };

            var headerRect = GUILayoutUtility.GetRect(columnWidth, 24);
            var headerBg = ColumnColors[idx];
            headerBg.a = 0.6f;
            EditorGUI.DrawRect(headerRect, headerBg);
            GUI.Label(headerRect, headerText, headerStyle);

            // Column body
            var dropRect = GUILayoutUtility.GetRect(columnWidth, position.height - 90,
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            // Draw column background
            var bgColor = ColumnColors[idx];
            if (hoverColumn == status && draggingTask != null)
            {
                bgColor = new Color(0.4f, 0.7f, 0.4f, 0.4f); // Highlight when dragging over
            }
            EditorGUI.DrawRect(dropRect, bgColor);

            // Track hover for drop target
            if (dropRect.Contains(Event.current.mousePosition))
            {
                hoverColumn = status;
            }

            // Scrollable content
            float contentHeight = GetContentHeight(filteredTasks.Count);
            scroll[idx] = GUI.BeginScrollView(dropRect, scroll[idx],
                new Rect(0, 0, dropRect.width - 16, contentHeight));

            float y = 4;
            foreach (var task in filteredTasks.OrderBy(t => t.priority))
            {
                var cardRect = new Rect(4, y, dropRect.width - 24, 80);
                DrawCard(task, cardRect, status);
                y += cardRect.height + 6;
            }

            GUI.EndScrollView();
        }
    }

    private string GetStatusIcon(KanbanStatus status)
    {
        return status switch
        {
            KanbanStatus.Backlog => "📋",
            KanbanStatus.Todo => "📝",
            KanbanStatus.Doing => "🔨",
            KanbanStatus.Done => "✅",
            _ => ""
        };
    }

    private float GetContentHeight(int count)
    {
        return Mathf.Max(100, count * 86 + 10);
    }

    private bool MatchesSearch(KanbanTask t)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;
        var s = search.Trim().ToLowerInvariant();
        return (t.title ?? "").ToLowerInvariant().Contains(s)
            || (t.notes ?? "").ToLowerInvariant().Contains(s)
            || (t.tags ?? "").ToLowerInvariant().Contains(s)
            || (t.link ?? "").ToLowerInvariant().Contains(s);
    }

    private void DrawCard(KanbanTask task, Rect r, KanbanStatus currentColumn)
    {
        // Card background
        var cardBg = EditorGUIUtility.isProSkin
            ? new Color(0.22f, 0.22f, 0.22f, 1f)
            : new Color(0.95f, 0.95f, 0.95f, 1f);

        // Highlight if being dragged
        if (draggingTask == task)
        {
            cardBg = new Color(0.3f, 0.4f, 0.5f, 0.8f);
        }

        EditorGUI.DrawRect(r, cardBg);

        // Priority indicator bar on left
        var priorityColor = PriorityColors[Mathf.Clamp(task.priority, 0, 3)];
        EditorGUI.DrawRect(new Rect(r.x, r.y, 4, r.height), priorityColor);

        // Title
        var titleRect = new Rect(r.x + 10, r.y + 4, r.width - 16, 18);
        var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
        GUI.Label(titleRect, TruncateString(task.title, 30), titleStyle);

        // Tags (if any)
        if (!string.IsNullOrWhiteSpace(task.tags))
        {
            var tagsRect = new Rect(r.x + 10, r.y + 22, r.width - 16, 14);
            var tagStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Italic,
                normal = { textColor = new Color(0.6f, 0.7f, 0.9f) }
            };
            GUI.Label(tagsRect, "🏷 " + task.tags, tagStyle);
        }

        // Notes preview
        var notesRect = new Rect(r.x + 10, r.y + 36, r.width - 16, 20);
        var notesStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            wordWrap = true,
            normal = { textColor = EditorGUIUtility.isProSkin ? Color.gray : new Color(0.4f, 0.4f, 0.4f) }
        };
        var notesText = string.IsNullOrWhiteSpace(task.notes) ? "—" : TruncateString(task.notes, 50);
        GUI.Label(notesRect, notesText, notesStyle);

        // Buttons row
        float btnY = r.y + r.height - 22;
        float btnW = (r.width - 20) / 4f;

        // Move left button (if not in first column)
        GUI.enabled = currentColumn != KanbanStatus.Backlog;
        if (GUI.Button(new Rect(r.x + 6, btnY, btnW - 2, 18), "◀"))
        {
            task.status = (KanbanStatus)((int)task.status - 1);
            GUI.changed = true;
        }
        GUI.enabled = true;

        // Edit button
        if (GUI.Button(new Rect(r.x + 6 + btnW, btnY, btnW - 2, 18), "Edit"))
        {
            KanbanTaskEditor.Open(board, task);
        }

        // Link button (if has link)
        GUI.enabled = !string.IsNullOrWhiteSpace(task.link);
        if (GUI.Button(new Rect(r.x + 6 + btnW * 2, btnY, btnW - 2, 18), "🔗"))
        {
            TryOpenLink(task.link);
        }
        GUI.enabled = true;

        // Move right button (if not in last column)
        GUI.enabled = currentColumn != KanbanStatus.Done;
        if (GUI.Button(new Rect(r.x + 6 + btnW * 3, btnY, btnW - 2, 18), "▶"))
        {
            task.status = (KanbanStatus)((int)task.status + 1);
            GUI.changed = true;
        }
        GUI.enabled = true;

        // Start drag on mouse down inside card (but not on buttons)
        var dragZone = new Rect(r.x, r.y, r.width, r.height - 24);
        if (Event.current.type == EventType.MouseDown && dragZone.Contains(Event.current.mousePosition))
        {
            draggingTask = task;
            dragSourceColumn = (int)task.status;

            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = new Object[] { board };
            DragAndDrop.SetGenericData("KANBAN_TASK", task);
            DragAndDrop.StartDrag("📋 " + task.title);
            Event.current.Use();
        }
    }

    private string TruncateString(string s, int maxLen)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length <= maxLen ? s : s.Substring(0, maxLen - 3) + "...";
    }

    private void HandleDragAndDrop()
    {
        var evt = Event.current;

        if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
        {
            var task = DragAndDrop.GetGenericData("KANBAN_TASK") as KanbanTask;
            if (task == null) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Move;

            if (evt.type == EventType.DragPerform && hoverColumn.HasValue)
            {
                DragAndDrop.AcceptDrag();
                task.status = hoverColumn.Value;
                draggingTask = null;
                hoverColumn = null;
                GUI.changed = true;
            }

            evt.Use();
            Repaint();
        }

        if (evt.type == EventType.DragExited || evt.type == EventType.MouseUp)
        {
            draggingTask = null;
            hoverColumn = null;
            Repaint();
        }
    }

    private void TryOpenLink(string link)
    {
        if (string.IsNullOrWhiteSpace(link)) return;
        if (link.StartsWith("http"))
        {
            Application.OpenURL(link);
        }
        else
        {
            Debug.Log($"[Kanban] Link: {link}");
        }
    }
}

// Task editor popup
public class KanbanTaskEditor : EditorWindow
{
    private KanbanBoard board;
    private KanbanTask task;
    private Vector2 notesScroll;

    private static readonly string[] PriorityLabels = { "🔴 Critical", "🟠 High", "🔵 Normal", "⚪ Low" };

    public static void Open(KanbanBoard b, KanbanTask t)
    {
        var w = GetWindow<KanbanTaskEditor>(true, "Edit Task", true);
        w.board = b;
        w.task = t;
        w.minSize = new Vector2(400, 380);
        w.maxSize = new Vector2(600, 500);
        w.ShowUtility();
    }

    private void OnGUI()
    {
        if (board == null || task == null)
        {
            Close();
            return;
        }

        EditorGUILayout.Space(8);

        // Title
        EditorGUILayout.LabelField("Title", EditorStyles.boldLabel);
        task.title = EditorGUILayout.TextField(task.title);

        EditorGUILayout.Space(4);

        // Status and Priority in one row
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
                task.status = (KanbanStatus)EditorGUILayout.EnumPopup(task.status);
            }
            GUILayout.Space(10);
            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField("Priority", EditorStyles.boldLabel);
                task.priority = EditorGUILayout.Popup(task.priority, PriorityLabels);
            }
        }

        EditorGUILayout.Space(4);

        // Tags
        EditorGUILayout.LabelField("Tags (comma separated)", EditorStyles.boldLabel);
        task.tags = EditorGUILayout.TextField(task.tags);

        EditorGUILayout.Space(4);

        // Link
        EditorGUILayout.LabelField("Link (URL or reference)", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            task.link = EditorGUILayout.TextField(task.link);
            GUI.enabled = !string.IsNullOrWhiteSpace(task.link) && task.link.StartsWith("http");
            if (GUILayout.Button("Open", GUILayout.Width(50)))
            {
                Application.OpenURL(task.link);
            }
            GUI.enabled = true;
        }

        EditorGUILayout.Space(4);

        // Notes
        EditorGUILayout.LabelField("Notes", EditorStyles.boldLabel);
        notesScroll = EditorGUILayout.BeginScrollView(notesScroll, GUILayout.Height(120));
        task.notes = EditorGUILayout.TextArea(task.notes, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(8);

        // Buttons
        using (new EditorGUILayout.HorizontalScope())
        {
            var deleteStyle = new GUIStyle(GUI.skin.button);
            deleteStyle.normal.textColor = new Color(0.9f, 0.3f, 0.3f);

            if (GUILayout.Button("Delete Task", deleteStyle))
            {
                if (EditorUtility.DisplayDialog("Delete Task",
                    $"Delete \"{task.title}\"?", "Delete", "Cancel"))
                {
                    board.tasks.Remove(task);
                    EditorUtility.SetDirty(board);
                    AssetDatabase.SaveAssets();
                    Close();
                    return;
                }
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Save & Close", GUILayout.Width(100)))
            {
                EditorUtility.SetDirty(board);
                AssetDatabase.SaveAssets();
                Close();
            }
        }

        if (GUI.changed)
        {
            EditorUtility.SetDirty(board);
        }
    }
}
