# TUnit Test Developers Guide

A comprehensive guide for writing clean, maintainable, and efficient tests using the TUnit testing framework.

## Table of Contents

1. [Getting Started](#getting-started)
2. [Running Tests](#running-tests)
3. [Creating Tests](#creating-tests)
4. [Assertions](#assertions)
5. [Data-Driven Testing](#data-driven-testing)
6. [Test Organization & Clean Code](#test-organization--clean-code)
7. [Test Lifecycle Management](#test-lifecycle-management)
8. [Advanced Patterns](#advanced-patterns)

## Getting Started

### Installation

#### Recommended Method

```bash
dotnet new install TUnit.Templates
dotnet new TUnit -n "YourTestProject"
```

## Running Tests

### Command Line Execution

#### Basic Execution

```bash
# Simple run
dotnet run

# With configuration
dotnet run -c Release --report-trx --coverage

# Using dotnet test (flags after --)
dotnet test -c Release -- --report-trx --coverage

# Run from built DLL
dotnet exec YourTestProject.dll --report-trx --coverage
```

### Test Filtering

TUnit supports filtering tests via the `--treenode-filter` flag:

```bash
# Run all tests in LoginTests class
dotnet run --treenode-filter /*/*/LoginTests/*

# Run specific test by name
dotnet run --treenode-filter /*/*/*/AcceptCookiesTest

# Filter by custom properties
dotnet run --treenode-filter /*/*/*/*[Category=Integration]
```

Filter syntax: `/<Assembly>/<Namespace>/<Class name>/<Test name>`

## Creating Tests

### Basic Test Structure

```csharp
using TUnit.Core;
using TUnit.Assertions;

namespace MyTestProject;

public class CalculatorTests
{
    [Test]
    public async Task Addition_Should_Return_Correct_Sum()
    {
        // Arrange
        var calculator = new Calculator();

        // Act
        var result = calculator.Add(2, 3);

        // Assert
        await Assert.That(result).IsEqualTo(5);
    }
}
```

### Key Principles

1. **Always use async/await**: Tests should be `async` and return `Task`
2. **Follow AAA pattern**: Arrange, Act, Assert
3. **Test isolation**: Each test gets a new instance of the test class
4. **Parallelization by default**: Tests run in parallel unless marked with `[NotInParallel]`

### Test Organization Best Practices

Following the testing guidelines:

```csharp
namespace MyApp.Tests;

public class UserServiceTests
{
    // Group related tests in nested classes
    public class CreateUserTests
    {
        [Test]
        [Property("Category", "Integration")]
        public async Task Should_Create_User_With_Valid_Data()
        {
            // Test implementation
        }

        [Test]
        [Property("Category", "Validation")]
        public async Task Should_Reject_Invalid_Email()
        {
            // Test implementation
        }
    }

    public class DeleteUserTests
    {
        // Related deletion tests
    }
}
```

## Assertions

### Basic Assertions

```csharp
// Simple assertion
await Assert.That(result).IsEqualTo(expected);

// Null checks
await Assert.That(result).IsNotNull();

// Type checks
await Assert.That(result).IsTypeOf<string>();

// Collection assertions
await Assert.That(collection).Contains(item);
await Assert.That(collection).HasCount(5);
```

### Complex Assertions with AND Conditions

Chain multiple assertions for comprehensive validation:

```csharp
await Assert.That(result)
    .IsNotNull()
    .And.IsPositive()
    .And.IsLessThan(100)
    .And.IsEqualTo(42);
```

### OR Conditions

Specify multiple acceptable outcomes:

```csharp
await Assert.That(statusCode)
    .IsEqualTo(200)
    .Or.IsEqualTo(201)
    .Or.IsEqualTo(202);
```

### Assertion Scopes

Aggregate multiple assertion failures instead of stopping at the first:

```csharp
[Test]
public async Task Should_Validate_Multiple_Properties()
{
    var user = GetUser();

    using (Assert.Multiple())
    {
        await Assert.That(user.Name).IsNotNull();
        await Assert.That(user.Email).Contains("@");
        await Assert.That(user.Age).IsGreaterThan(0);
    }
    // All assertion failures will be reported together
}
```

### Assertion Groups for Complex Logic

For complex assertion combinations:

```csharp
var primaryCondition = AssertionGroup.For(response)
    .WithAssertion(assert => assert.StatusCode.IsEqualTo(200))
    .And(assert => assert.Content.IsNotNull());

var fallbackCondition = AssertionGroup.ForSameValueAs(primaryCondition)
    .WithAssertion(assert => assert.StatusCode.IsEqualTo(202))
    .And(assert => assert.RetryAfter.IsGreaterThan(0));

await AssertionGroup.Assert(primaryCondition).Or(fallbackCondition);
```

### Custom Assertions

Following the guideline "Use custom assertions when it makes sense":

```csharp
public static class CustomAssertions
{
    public static InvokableValueAssertionBuilder<User> HasValidEmail(
        this IValueSource<User> valueSource)
    {
        return valueSource.RegisterAssertion(
            new EmailValidationAssertion()
        );
    }
}

// Usage
await Assert.That(user).HasValidEmail();
```

## Data-Driven Testing

### Using Arguments Attribute

```csharp
[Test]
[Arguments(1, 1, 2)]
[Arguments(2, 3, 5)]
[Arguments(10, -5, 5)]
public async Task Addition_Should_Calculate_Correctly(
    int a, int b, int expected)
{
    var result = calculator.Add(a, b);
    await Assert.That(result).IsEqualTo(expected);
}
```

### Method Data Source

For dynamic test data:

```csharp
public class CalculatorTests
{
    [Test]
    [MethodDataSource(typeof(TestData), nameof(TestData.AdditionCases))]
    public async Task Addition_With_Complex_Data(AdditionTestCase testCase)
    {
        var result = calculator.Add(testCase.A, testCase.B);
        await Assert.That(result).IsEqualTo(testCase.Expected);
    }
}

public static class TestData
{
    public static IEnumerable<Func<AdditionTestCase>> AdditionCases()
    {
        yield return () => new AdditionTestCase { A = 1, B = 1, Expected = 2 };
        yield return () => new AdditionTestCase { A = -1, B = 1, Expected = 0 };
        yield return () => new AdditionTestCase { A = int.MaxValue, B = 0, Expected = int.MaxValue };
    }
}
```

### Class Data Source with Dependency Injection

```csharp
[Test]
[ClassDataSource<DatabaseFixture>(Shared = SharedType.PerTestSession)]
[ClassDataSource<TestUser>(Shared = SharedType.None)]
public async Task Database_Operation_Test(
    DatabaseFixture db,
    TestUser user)
{
    // Use injected instances
    await db.InsertUser(user);
    var retrieved = await db.GetUser(user.Id);
    await Assert.That(retrieved).IsNotNull();
}
```

## Test Organization & Clean Code

### Following the Testing Guidelines

#### 1. Group Related Tests in Nested Classes

```csharp
public class OrderServiceTests
{
    public class CreateOrderTests
    {
        [Test]
        public async Task Should_Create_Order_With_Valid_Items() { }

        [Test]
        public async Task Should_Reject_Empty_Order() { }
    }

    public class CancelOrderTests
    {
        [Test]
        public async Task Should_Cancel_Pending_Order() { }

        [Test]
        public async Task Should_Not_Cancel_Shipped_Order() { }
    }
}
```

#### 2. Use Builder Pattern for Test Data

```csharp
public class UserBuilder
{
    private string _name = "Default User";
    private string _email = "user@example.com";
    private int _age = 25;

    public UserBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public UserBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    public User Build() => new User(_name, _email, _age);
}

// Usage in tests
[Test]
public async Task Test_User_Creation()
{
    var user = new UserBuilder()
        .WithName("John Doe")
        .WithEmail("john@example.com")
        .Build();

    // Test with clean, readable test data
}
```

#### 3. Write Clean Tests with Fixtures

```csharp
public class ApiTestFixture : IAsyncInitializer, IAsyncDisposable
{
    public HttpClient Client { get; private set; }
    public TestDatabase Database { get; private set; }

    public async Task InitializeAsync()
    {
        Database = await TestDatabase.CreateAsync();
        Client = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        await Database?.DisposeAsync();
    }
}

[Test]
[ClassDataSource<ApiTestFixture>(Shared = SharedType.PerClass)]
public async Task Api_Should_Return_User(ApiTestFixture fixture)
{
    // Clean test using fixture
    var response = await fixture.Client.GetAsync("/users/1");
    await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
}
```

#### 4. Regression Testing

```csharp
[Test]
[Property("Type", "Regression")]
[Property("Issue", "BUG-1234")]
[DisplayName("Regression: Should handle null reference in user service")]
public async Task UserService_Should_Handle_Null_User_Gracefully()
{
    // Test that validates the bug fix
    var service = new UserService();
    var result = await service.ProcessUser(null);

    await Assert.That(result).IsNotNull();
    await Assert.That(result.IsSuccess).IsFalse();
    await Assert.That(result.Error).Contains("User cannot be null");
}
```

## Test Lifecycle Management

### Setup Methods

```csharp
public class DatabaseTests
{
    private TestDatabase _database;
    private static HttpClient _httpClient;

    [Before(Test)]
    public async Task SetupTest()
    {
        _database = await TestDatabase.CreateAsync();
    }

    [Before(Class)]
    public static async Task SetupClass()
    {
        _httpClient = new HttpClient();
        await _httpClient.GetAsync("https://api.example.com/ping");
    }

    [Before(Assembly)]
    public static async Task SetupAssembly()
    {
        // One-time assembly setup
        await MigrationsRunner.RunAsync();
    }
}
```

### Cleanup Methods

```csharp
public class ResourceTests : IAsyncDisposable
{
    private TempFile _tempFile;

    [After(Test)]
    public async Task CleanupTest()
    {
        await _tempFile?.DeleteAsync();
    }

    [After(Class)]
    public static async Task CleanupClass()
    {
        await TestCache.ClearAsync();
    }

    // Or implement IAsyncDisposable
    public async ValueTask DisposeAsync()
    {
        await _tempFile?.DeleteAsync();
    }
}
```

### Global Hooks

Create a `GlobalHooks.cs` file:

```csharp
public static class GlobalHooks
{
    [BeforeEvery(Test)]
    public static async Task BeforeEveryTest(TestContext context)
    {
        Console.WriteLine($"Starting test: {context.TestInformation.TestName}");
    }

    [AfterEvery(Test)]
    public static async Task AfterEveryTest(TestContext context)
    {
        if (context.Result.Status == TestStatus.Failed)
        {
            await LogFailureDetails(context);
        }
    }
}
```

## Advanced Patterns

### Dependency Injection

```csharp
public class MicrosoftDIDataSource : DependencyInjectionDataSourceAttribute<IServiceScope>
{
    protected override IServiceScope CreateScope()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase("TestDb"));

        var provider = services.BuildServiceProvider();
        return provider.CreateScope();
    }

    protected override object? Create(IServiceScope scope, Type type)
    {
        return scope.ServiceProvider.GetService(type);
    }
}

[MicrosoftDIDataSource]
public class UserServiceTests(IUserService userService, AppDbContext dbContext)
{
    [Test]
    public async Task Should_Create_User()
    {
        // Use injected services
        var user = await userService.CreateAsync("John", "john@example.com");
        await Assert.That(user).IsNotNull();
    }
}
```

### Custom Display Names

```csharp
[Test]
[Arguments("john@example.com", true)]
[Arguments("invalid-email", false)]
[DisplayName("Email validation: '$email' should be $expectedValid")]
public async Task Validate_Email(string email, bool expectedValid)
{
    var isValid = EmailValidator.IsValid(email);
    await Assert.That(isValid).IsEqualTo(expectedValid);
}
```

### Parallel Execution Control

```csharp
[NotInParallel("database-tests")]
public class DatabaseIntegrationTests
{
    [Test]
    [NotInParallel(Order = 1)]
    public async Task First_Database_Operation() { }

    [Test]
    [NotInParallel(Order = 2)]
    public async Task Second_Database_Operation() { }
}
```

### Test Categories and Properties

```csharp
[Test]
[Property("Category", "Integration")]
[Property("Priority", "High")]
[Property("Owner", "TeamA")]
public async Task Critical_Integration_Test()
{
    // Test implementation
}

// Run only integration tests
// dotnet run --treenode-filter /*/*/*/*[Category=Integration]
```

## Best Practices Summary

1. **Always use async/await** - All tests should be async and properly await assertions
2. **Test Isolation** - Each test gets a new instance; avoid shared state
3. **Data-Driven Testing** - Use Arguments, MethodDataSource, or ClassDataSource for test cases
4. **Clean Test Code** - Treat test code with the same care as production code
5. **Builder Pattern** - Use builders for complex test data setup
6. **Proper Organization** - Group related tests in nested classes
7. **Custom Assertions** - Create custom assertions for repeated complex validations
8. **Regression Tests** - Write tests for bugs to prevent regressions
9. **Test Categories** - Use properties for test organization and filtering
10. **Assertion Scopes** - Use `Assert.Multiple()` to see all failures at once

## Common Pitfalls to Avoid

1. **Forgetting to await assertions** - Always await `Assert.That()` calls
2. **Using Microsoft.NET.Test.Sdk** - TUnit has its own test platform
3. **Shared instance state** - Remember each test gets a new instance
4. **Not considering parallelization** - Tests run in parallel by default
5. **Complex assertion logic without groups** - Use AssertionGroup for complex conditions
6. **Not using data-driven tests** - Leverage data sources for similar test cases
7. **Ignoring test organization** - Use nested classes and categories

## Conclusion

TUnit provides a modern, efficient testing framework with powerful features for creating maintainable test suites. By following these guidelines and leveraging TUnit's capabilities, you can create clean, readable, and reliable tests that serve as both verification and documentation for your code.

Remember: Test code is production code. Apply the same standards of quality, maintainability, and thoughtfulness to your tests as you would to your application code.
