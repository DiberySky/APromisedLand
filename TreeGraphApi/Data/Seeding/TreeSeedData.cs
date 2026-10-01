namespace TreeGraphApi.Data.Seeding;

/// <summary>
/// 演示用种子数据。可按业务需要替换。
/// </summary>
public static class TreeSeedData
{
    public static List<SeedNode> GetNodes() => new()
    {
        new SeedNode
        {
            Text = "Documents", Icon = "folder", NodeType = "folder", SortOrder = 1,
            Children =
            {
                new SeedNode
                {
                    Text = "Work", Icon = "folder", NodeType = "folder", SortOrder = 1,
                    Children =
                    {
                        new SeedNode
                        {
                            Text = "Projects", Icon = "folder", NodeType = "folder", SortOrder = 1,
                            Children =
                            {
                                new SeedNode { Text = "Q1-Report.docx", NodeType = "file", SortOrder = 1 },
                                new SeedNode { Text = "Q2-Report.docx", NodeType = "file", SortOrder = 2 },
                                new SeedNode { Text = "Budget.xlsx",    NodeType = "file", SortOrder = 3 },
                            }
                        },
                        new SeedNode
                        {
                            Text = "Meetings", Icon = "folder", NodeType = "folder", SortOrder = 2,
                            Children =
                            {
                                new SeedNode { Text = "2024-01-Kickoff.md", NodeType = "file", SortOrder = 1 },
                                new SeedNode { Text = "2024-03-Review.md",  NodeType = "file", SortOrder = 2 },
                            }
                        },
                    }
                },
                new SeedNode
                {
                    Text = "Personal", Icon = "folder", NodeType = "folder", SortOrder = 2,
                    Children =
                    {
                        new SeedNode { Text = "Resume.pdf",   NodeType = "file", SortOrder = 1 },
                        new SeedNode { Text = "Tax-2024.pdf", NodeType = "file", SortOrder = 2 },
                    }
                },
            }
        },
        new SeedNode
        {
            Text = "Images", Icon = "folder", NodeType = "folder", SortOrder = 2,
            Children =
            {
                new SeedNode
                {
                    Text = "Screenshots", Icon = "folder", NodeType = "folder", SortOrder = 1,
                    Children =
                    {
                        new SeedNode { Text = "shot-001.png", NodeType = "file", SortOrder = 1 },
                        new SeedNode { Text = "shot-002.png", NodeType = "file", SortOrder = 2 },
                    }
                },
                new SeedNode { Text = "wallpaper.jpg", NodeType = "file", SortOrder = 2 },
            }
        },
        new SeedNode
        {
            Text = "Music", Icon = "folder", NodeType = "folder", SortOrder = 3,
            Children =
            {
                new SeedNode { Text = "track-01.mp3", NodeType = "file", SortOrder = 1 },
                new SeedNode { Text = "track-02.mp3", NodeType = "file", SortOrder = 2 },
            }
        },
        new SeedNode { Text = "README.md", NodeType = "file", SortOrder = 4 },
    };
}
