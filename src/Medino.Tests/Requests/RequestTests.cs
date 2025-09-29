namespace Medino.Tests.Requests;

[Collection("Mediator Tests")]
public class RequestTests
{
    private readonly IMediator _mediator;

    public RequestTests(GlobalSetup setup)
    {
        _mediator = setup.Mediator;
    }

    [Fact]
    public async Task GivenARequestIsCreated_WhenItIsSent_ThenTheHandlerReturnsAResponse()
    {
        var request = new TestRequest();

        var response = await _mediator.SendAsync(request);

        Assert.NotNull(response);
        Assert.Equal("Success", response.Message);
    }
}