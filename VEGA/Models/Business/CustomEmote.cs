namespace Models;

public class CustomEmote
{
    public CustomEmote(bool animated, string id, string name, string filename, string url)
    {
        Animated = animated;
        Id = id;
        Name = name;
        Url = url;
        Filename = filename;
    }

    public bool Animated;
    public string Id;
    public string Name;
    public string Filename;
    public string Url;
}