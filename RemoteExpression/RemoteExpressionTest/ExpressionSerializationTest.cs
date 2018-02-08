using ExpressionSerialization;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace RemoteExpressionTest
{
    public class ExpressionSerializationTest
    {
        public interface ISomeEntity
        {
            Guid Id { get; }
            int Index { get; }
            string Name { get; }
            IEnumerable<Guid> References { get; }
        }
        static int index = 0;
        public class SomeEntity : ISomeEntity
        {
            public Guid Id { get; } = Guid.NewGuid();

            public int Index { get; } = ++index;

            public string Name { get; set; }

            public IEnumerable<Guid> References { get; set; }
        }

        public IEnumerable<ISomeEntity> theCollectionDescription = Enumerable.Empty<ISomeEntity>();

        public IEnumerable<SomeEntity> theConcreteCollection = new[] {
            new SomeEntity{Name="tralalala", References = new [] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()}},
            new SomeEntity{Name="lalala", References = new [] {Guid.NewGuid(),Guid.NewGuid()}},
        };


        [Fact]
        public void TestSome()
        {
            Assert.False(false);
            Assert.True(true);
        }

        [Fact]
        public void TestSerialization()
        {
            var resolver = new Mock<IDataSourceResolver>();
            resolver.Setup(r => r.ResolveDataSource(It.IsAny<Type>())).Returns(theConcreteCollection.Cast<ISomeEntity>().AsQueryable());
            var xml = theCollectionDescription.AsQueryable().Where(e => e.Name=="lalala").SelectMany(e => e.References.Select(r => new {r, e.Index})).Serialize();
            Assert.NotNull(xml);
            var expression = xml.Deserialize(resolver.Object);
            var result = expression.Compile().DynamicInvoke();// as IQueryable<ISomeEntity>;
        }
    }
}
