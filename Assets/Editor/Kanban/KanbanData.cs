using System;
using System.Collections.Generic;
using UnityEngine;

public enum KanbanStatus { Backlog, Todo, Doing, Done }

[Serializable]
public class KanbanTask
{
    public string id = Guid.NewGuid().ToString("N");
    public string title = "New task";
    [TextArea(2, 8)] public string notes;
    public KanbanStatus status = KanbanStatus.Todo;

    // Optional:
    public int priority = 2; // 0 high ... 3 low
    public string tags;      // "ui,ads,build"
    public string link;      // github issue url or "#123"
}

public class KanbanBoard : ScriptableObject
{
    public List<KanbanTask> tasks = new();
}
