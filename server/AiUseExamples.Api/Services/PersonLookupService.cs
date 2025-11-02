using Microsoft.Extensions.Logging;

namespace AiUseExamples.Api.Services;

public interface IPersonLookupService
{
    Task<List<PersonLookupResult>> LookupPersonsAsync(List<string> nameStrings);
}

public class PersonLookupService : IPersonLookupService
{
    private readonly ILogger<PersonLookupService> _logger;
    private readonly List<Person> _mockPersons;

    public PersonLookupService(ILogger<PersonLookupService> logger)
    {
        _logger = logger;
        _mockPersons = new List<Person>
        {
            new Person { FullName = "Aleksander Valle Grunnvoll", Email = "aleksander.valle@example.com" },
            new Person { FullName = "Erik Johansen", Email = "erik.johansen@example.com" },
            new Person { FullName = "Anna Hansen", Email = "anna.hansen@example.com" },
            new Person { FullName = "Ole Pedersen", Email = "ole.pedersen@example.com" },
            new Person { FullName = "Kari Nilsen", Email = "kari.nilsen@example.com" },
            new Person { FullName = "Lars Andersen", Email = "lars.andersen@example.com" },
            new Person { FullName = "Maria Berg", Email = "maria.berg@example.com" },
            new Person { FullName = "Tommy Solberg", Email = "tommy.solberg@example.com" },
            new Person { FullName = "Ingrid Larsen", Email = "ingrid.larsen@example.com" },
            new Person { FullName = "Bjørn Haugen", Email = "bjorn.haugen@example.com" },
            new Person { FullName = "Sofie Kristensen", Email = "sofie.kristensen@example.com" },
            new Person { FullName = "Morten Dahl", Email = "morten.dahl@example.com" },
            new Person { FullName = "Hanne Svendsen", Email = "hanne.svendsen@example.com" },
            new Person { FullName = "Per Moen", Email = "per.moen@example.com" },
            new Person { FullName = "Linda Holmen", Email = "linda.holmen@example.com" },
            new Person { FullName = "Andreas Foss", Email = "andreas.foss@example.com" },
            new Person { FullName = "Camilla Ødegård", Email = "camilla.odegard@example.com" },
            new Person { FullName = "Trond Bakken", Email = "trond.bakken@example.com" },
            new Person { FullName = "Martine Strand", Email = "martine.strand@example.com" },
            new Person { FullName = "Stian Aas", Email = "stian.aas@example.com" }
        };
    }

    public Task<List<PersonLookupResult>> LookupPersonsAsync(List<string> nameStrings)
    {
        var results = new List<PersonLookupResult>();

        foreach (var nameString in nameStrings)
        {
            var matchedPersons = _mockPersons
                .Where(p => p.FullName.Contains(nameString, StringComparison.OrdinalIgnoreCase) ||
                           p.FullName.Split(' ').Any(name => name.Contains(nameString, StringComparison.OrdinalIgnoreCase)))
                .Select(p => new PersonLookupResult
                {
                    FullName = p.FullName,
                    Email = p.Email
                })
                .ToList();

            results.AddRange(matchedPersons);
        }

        // Remove duplicates
        results = results
            .GroupBy(p => p.Email)
            .Select(g => g.First())
            .ToList();

        _logger.LogInformation("Looked up {Count} persons for input: {Names}", results.Count, string.Join(", ", nameStrings));

        return Task.FromResult(results);
    }
}

public class Person
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class PersonLookupResult
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

