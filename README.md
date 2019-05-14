# Lucene.Net.Linq (LINQ to Lucene.Net)

Lucene.Net.Linq is a .net library that enables LINQ queries to run natively on a Lucene.Net index.

* Automatically converts PONOs to Documents and back
* Add, delete and update documents in atomic transaction
* Unit of Work pattern automatically tracks and flushes updated documents
* Update/replace documents with \[Field(Key=true)\] to prevent duplicates
* Term queries
* Prefix queries
* Range queries and numeric range queries
* Complex boolean queries
* Native pagination using Skip and Take
* Support storing and querying NumericField
* Automatically convert complex types for storing, querying and sorting
* Custom boost functions using IQueryable<T>.Boost() extension method
* Sort by standard string, NumericField or any type that implements IComparable
* Sort by item.Score() extension method to sort by relevance
* Specify custom format for DateTime stored as strings
* Register cache-warming queries to be executed when IndexSearcher is being reloaded

## Builds

[![Build status](https://ci.appveyor.com/api/projects/status/voelauhwvv1l8j2f)](https://ci.appveyor.com/project/chriseldredge/lucene-net-linq)

## Available on NuGet Gallery

To install the [Lucene.Net.Linq package](http://nuget.org/packages/Lucene.Net.Linq),
run the following command in the [Package Manager Console](http://docs.nuget.org/docs/start-here/using-the-package-manager-console)

    PM> Install-Package Lucene.Net.Linq

## Examples

1. Using [fluent syntax](source/Lucene.Net.Linq.Tests/Samples/FluentConfiguration.cs) to configure mappings
1. Using [attributes](source/Lucene.Net.Linq.Tests/Samples/AttributeConfiguration.cs) to configure mappings
1. Specifying [document keys](source/Lucene.Net.Linq.Tests/Samples/DocumentKeys.cs)

## Note on Performance

Initial versions of the library include a query filter when your entites specify a document key or key field
in their mappings. The intention of this filter is to ensure that multiple entity types can be stored in a
single index without unexpected errors.

It has been pointed out that this query filter adds significant overhead to query performance and goes
against a best practive of using a different index for each type of document being stored.

To maintain backwards compatibility, the feature is left enabled by default, but it can now be disabled
by doing:

    luceneDataProvider.Settings.EnableMultipleEntities = false;

Future versions of this library may change the default behavior.

## Integration with OData

Lucene.Net.Linq supports both [WCF Data Services](http://msdn.microsoft.com/en-us/library/cc668792.aspx)
and [WebApi OData](http://www.asp.net/web-api/overview/odata-support-in-aspnet-web-api). These libraries
by default support a feature known as Null Propagation that adds null safety to LINQ Expressions to
avoid NullReferenceException from being thrown when operating on a property that may be null.

A simple expression like:

```c#
from doc in Documents where doc.Name.StartsWith("Sample") select doc;
```

Is translated into:

```c#
from doc in Documents where (doc != null && doc.Name != null
    && doc.Name.StartsWith("Sample")) select doc;
```

Null Propagation is designed to work with [LINQ To Objects](http://msdn.microsoft.com/en-ca/library/bb397919.aspx)
but is not required for LINQ providers such as Lucene.Net.Linq. Lucene.Net.Linq does its best to remove
these null-safety checks when translating a LINQ expression tree into a Lucene Query, but for best
performance it is recommended to simply turn the feature off, as in this example:

```c#
public class PackagesODataController : ODataController
{
    [EnableQuery(HandleNullPropagation = HandleNullPropagationOption.False)]
    public IQueryable<Package> Get()
    {
        return provider.AsQueryable<Package>();
    }
}
```

## Upcoming features / ideas / bugs / known issues

See Issues on the GitHub project page.

### Unsupported Characters in Indexed Properties

Some characters, even when using a KeywordAnalyzer or equivalent, will
not be handled correctly by Lucene.Net.Linq, such as `\`, `:`, `?` and `*`
because these characters have special meaning to Lucene's query parser.

This means if you want to index a DOS style path such as `c:\dos` and
later retrieve documents using the same term, it will not work properly.

These characters are perfectly fine for fields that will be analyzed
by a tokenizer that would remove them, but exact matching on the entire
value is not possible.

If exact matching is required, these characters should be replaced
with suitable substitutes that are not reserved by Lucene.



