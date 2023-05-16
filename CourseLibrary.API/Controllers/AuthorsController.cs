using AutoMapper;
using CourseLibrary.API.Helpers;
using CourseLibrary.API.Models;
using CourseLibrary.API.ResourceParameters;
using CourseLibrary.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Net.Http.Headers;
using System.Text.Json;

namespace CourseLibrary.API.Controllers;


[ApiController]
[Route("api/authors")]
public class AuthorsController : ControllerBase
{
    private readonly ICourseLibraryRepository _courseLibraryRepository;
    private readonly IMapper _mapper;
    private readonly IPropertyMappingService _propertyMappingService;
    private readonly IPropertyCheckerService _propertyCheckerService;
    private readonly ProblemDetailsFactory _problemDetailsFactory;


    public AuthorsController(
        ICourseLibraryRepository courseLibraryRepository,
        IMapper mapper,
        IPropertyMappingService mappingService,
        ProblemDetailsFactory problemDetailsFactory,
        IPropertyCheckerService propertyCheckerService)
    {
        _courseLibraryRepository = courseLibraryRepository ??
            throw new ArgumentNullException(nameof(courseLibraryRepository));
        _mapper = mapper ??
            throw new ArgumentNullException(nameof(mapper));
        _propertyMappingService = mappingService;
        _problemDetailsFactory = problemDetailsFactory;
        _propertyCheckerService = propertyCheckerService;
    }

    [HttpGet(Name = "GetAuthors")]
    [HttpHead]
    public async Task<IActionResult> GetAuthors(
        [FromQuery] AuthorsResourceParameters authorsResourceParameters)
    {
        //throw new Exception("Test exception");

        if (!_propertyMappingService
            .ValidMappingExistsFor<AuthorDto, Entities.Author>(
                authorsResourceParameters.OrderBy))
        {
            return BadRequest();
        }

        if (!_propertyCheckerService.TypeHasProperties<AuthorDto>
              (authorsResourceParameters.Fields))
        {
            return BadRequest(
                _problemDetailsFactory.CreateProblemDetails(HttpContext,
                    statusCode: 400,
                    detail: $"Not all requested data shaping fields exist on " +
                    $"the resource: {authorsResourceParameters.Fields}"));
        }

        // get authors from repo
        var authorsFromRepo = await _courseLibraryRepository
            .GetAuthorsAsync(authorsResourceParameters);


        var paginationMetadata = new
        {
            totalCount = authorsFromRepo.TotalCount,
            pageSize = authorsFromRepo.PageSize,
            currentPage = authorsFromRepo.CurrentPage,
            totalPages = authorsFromRepo.TotalPages
        };

        Response.Headers.Add("X-Pagination",
               JsonSerializer.Serialize(paginationMetadata));

        // create links
        var links = CreateLinksForAuthors(authorsResourceParameters,
            authorsFromRepo.HasNext,
            authorsFromRepo.HasPrevious);

        var shapedAuthors = _mapper.Map<IEnumerable<AuthorDto>>(authorsFromRepo)
                                .ShapeData(authorsResourceParameters.Fields);

        var shapedAuthorsWithLinks = shapedAuthors.Select(author =>
        {
            var authorAsDictionary = author as IDictionary<string, object?>;
            var authorLinks = CreateLinksForAuthor(
                (Guid)authorAsDictionary["Id"],
                null);
            authorAsDictionary.Add("links", authorLinks);
            return authorAsDictionary;
        });


        var linkedCollectionResource = new
        {
            value = shapedAuthorsWithLinks,
            links = links
        };

        // return them
        return Ok(linkedCollectionResource);
    }



    [HttpGet("{authorId}", Name = "GetAuthor")]
    public async Task<IActionResult> GetAuthor(
        Guid authorId, string? fields,
        [FromHeader(Name = "Accept")] string? mediaType)
    {
        if (!MediaTypeHeaderValue.TryParse(mediaType, out var parsedMediaType))
        {
            return BadRequest(
              _problemDetailsFactory.CreateProblemDetails(HttpContext,
                  statusCode: 400,
                  detail: $"Accept header media type value isnot valid media type."));
        }


        if (!_propertyCheckerService.TypeHasProperties<AuthorDto>
           (fields))
        {
            return BadRequest(
              _problemDetailsFactory.CreateProblemDetails(HttpContext,
                  statusCode: 400,
                  detail: $"Not all requested data shaping fields exist on " +
                  $"the resource: {fields}"));
        }

        // get author from repo
        var authorFromRepo = await _courseLibraryRepository.GetAuthorAsync(authorId);

        if (authorFromRepo == null)
        {
            return NotFound();
        }

        if (parsedMediaType.MediaType == "application/vnd.marvin.hateoas+json")
        {
            //create links
            var links = CreateLinksForAuthor(authorId, fields);

            var linkedResourceToReturn = _mapper.Map<AuthorDto>(authorFromRepo)
                .ShapeData(fields) as IDictionary<string, object?>;

            linkedResourceToReturn.Add("links", links);

            // return author
            return Ok(linkedResourceToReturn);
        }

        return Ok(_mapper.Map<AuthorDto>(authorFromRepo)
                .ShapeData(fields));

    }

    [HttpPost(Name = "CreateAuthor")]
    public async Task<ActionResult<AuthorDto>> CreateAuthor(AuthorForCreationDto author)
    {
        var authorEntity = _mapper.Map<Entities.Author>(author);

        _courseLibraryRepository.AddAuthor(authorEntity);
        await _courseLibraryRepository.SaveAsync();

        var authorToReturn = _mapper.Map<AuthorDto>(authorEntity);

        // create links
        var links = CreateLinksForAuthor(authorToReturn.Id, null);

        // add 
        var linkedResourceToReturn = authorToReturn.ShapeData(null)
            as IDictionary<string, object?>;
        linkedResourceToReturn.Add("links", links);

        return CreatedAtRoute("GetAuthor",
            new { authorId = linkedResourceToReturn["Id"] },
            linkedResourceToReturn);
    }

    [HttpOptions()]
    public IActionResult GetAuthorsOptions()
    {
        Response.Headers.Add("Allow", "GET,HEAD,POST,OPTIONS");
        return Ok();
    }

    private string? CreateAuthorsResourceUri(AuthorsResourceParameters parameters,
        ResourceUriType type)
    {
        switch (type)
        {
            case ResourceUriType.PreviousPage:
                return Url.Link("GetAuthors",
                    new
                    {
                        fields = parameters.Fields,
                        orderBy = parameters.OrderBy,
                        pageNumber = parameters.PageNumber - 1,
                        pageSize = parameters.PageSize,
                        mainCategory = parameters.MainCategory,
                        searchQuery = parameters.SearchQuery
                    });
            case ResourceUriType.NextPage:
                return Url.Link("GetAuthors",
                    new
                    {
                        fields = parameters.Fields,
                        orderBy = parameters.OrderBy,
                        pageNumber = parameters.PageNumber + 1,
                        pageSize = parameters.PageSize,
                        mainCategory = parameters.MainCategory,
                        searchQuery = parameters.SearchQuery
                    });
            case ResourceUriType.Current:
            default:
                return Url.Link("GetAuthors",
                    new
                    {
                        fields = parameters.Fields,
                        orderBy = parameters.OrderBy,
                        pageNumber = parameters.PageNumber,
                        pageSize = parameters.PageSize,
                        mainCategory = parameters.MainCategory,
                        searchQuery = parameters.SearchQuery
                    });
        }
    }

    private IEnumerable<LinkDto> CreateLinksForAuthors(AuthorsResourceParameters parameters,
        bool hasNext, bool hasPrevious)
    {
        var links = new List<LinkDto>();

        // self 
        links.Add(
            new(CreateAuthorsResourceUri(parameters, ResourceUriType.Current),
                "self",
                "GET"));

        if (hasNext)
        {
            links.Add(
                new(CreateAuthorsResourceUri(parameters, ResourceUriType.NextPage),
                "nextPage",
                "GET"));
        }

        if (hasPrevious)
        {
            links.Add(
                new(CreateAuthorsResourceUri(parameters, ResourceUriType.PreviousPage),
                "previousPage",
                "GET"));
        }

        return links;
    }

    private IEnumerable<LinkDto> CreateLinksForAuthor(Guid authorId, string? fields)
    {
        var links = new List<LinkDto>();

        if (string.IsNullOrWhiteSpace(fields))
        {
            links.Add(
              new LinkDto(Url.Link("GetAuthor", new { authorId }),
              "self",
              "GET"));
        }
        else
        {
            links.Add(
              new LinkDto(Url.Link("GetAuthor", new { authorId, fields }),
              "self",
              "GET"));
        }

        links.Add(
              new LinkDto(Url.Link("CreateCourseForAuthor", new { authorId }),
              "create_course_for_author",
              "POST"));
        links.Add(
             new LinkDto(Url.Link("GetCoursesForAuthor", new { authorId }),
             "courses",
             "GET"));

        return links;
    }
}