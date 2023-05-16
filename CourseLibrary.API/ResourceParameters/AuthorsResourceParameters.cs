namespace CourseLibrary.API.ResourceParameters;

public class AuthorsResourceParameters
{
    public string? MainCategory { get; set; }
    public string? SearchQuery { get; set; }
    public int PageNumber { get; set; } = 1;

    const int maxPageSize = 20;

    private int _pageSize = 10;
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = (value > maxPageSize) ? maxPageSize : value;
    }

    public string OrderBy { get; set; } = "Name";
    public string? Fields { get; set; } 
}