using Xunit;

namespace Stock.Trading.Integration.Tests
{
	[CollectionDefinition(nameof(TestContext))]
	public class TestContextCollection : ICollectionFixture<TestContext>
	{
		
	}
}