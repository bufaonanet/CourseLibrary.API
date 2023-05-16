namespace CourseLibrary.API.Services;

public interface IPropertyMappingService
{
    Dictionary<string, PropertyMappingValue> GetPropertyMapping<TSource, TDestination>();

    public bool ValidMappingExistsFor<TSource, TDestination>(string fields);
}