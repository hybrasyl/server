using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Hybrasyl.Tests;

[CollectionDefinition("Hybrasyl")]
public class HybrasylCollection : ICollectionFixture<HybrasylFixture> {}
