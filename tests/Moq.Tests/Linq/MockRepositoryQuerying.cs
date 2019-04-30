// Copyright (c) 2007, Clarius Consulting, Manas Technology Solutions, InSTEDD.
// All rights reserved. Licensed under the BSD 3-Clause License; see License.txt.

using System.Linq;

using Xunit;

namespace Moq.Tests.Linq
{
	public class MockRepositoryQuerying
	{
		public class GivenAStrictFactory
		{
			private MockRepository repository;

			public GivenAStrictFactory()
			{
				this.repository = new MockRepository(MockBehavior.Strict);
			}

			[Fact]
			public void WhenQueryingSingle_ThenItIsStrict()
			{
				var foo = this.repository.OneOf<IFoo>();

				Assert.Throws<MockException>(() => foo.Do());
			}

			[Fact]
			public void WhenQueryingMultiple_ThenItIsStrict()
			{
				var foo = this.repository.Of<IFoo>().First();

				Assert.Throws<MockException>(() => foo.Do());
			}

			[Fact]
			public void WhenQueryingSingleWithProperty_ThenItIsStrict()
			{
				var foo = this.repository.OneOf<IFoo>(x => x.Id == "1");

				Assert.Throws<MockException>(() => foo.Do());

				Mock.Get(foo).Verify();

				Assert.Equal("1", foo.Id);
			}

			[Fact]
			public void WhenQueryingMultipleWithProperty_ThenItIsStrict()
			{
				var foo = this.repository.Of<IFoo>(x => x.Id == "1").First();

				Assert.Throws<MockException>(() => foo.Do());

				Mock.Get(foo).VerifyAll();

				Assert.Equal("1", foo.Id);
			}

			[Fact]
			public void WhenQueryingMultipleWithProperty_ThenItIsStrict_()
			{
				var foo = Mock.Of<IFoo>(m => m.Id == "1");

				Mock.Get(foo).VerifyAll();
			}
		}

		public interface IFoo
		{
			string Id { get; set; }
			bool Do();
		}
	}
}
