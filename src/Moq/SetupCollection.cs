// Copyright (c) 2007, Clarius Consulting, Manas Technology Solutions, InSTEDD.
// All rights reserved. Licensed under the BSD 3-Clause License; see License.txt.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Moq
{
	internal sealed class InvokableSetup
	{
		internal struct Identifier
		{
			public int Id { get; }

			public Identifier(int id)
			{
				Id = id;
			}
		}

		private static InvokableSetup notRegistered = new InvokableSetup(-1, null);

		public Identifier Id { get; }
		public Setup Setup { get; }
		public bool CanVerify { get; }

		public InvokableSetup(int id, Setup setup)
		{
			this.Id = new Identifier(id);
			this.Setup = setup;
			this.CanVerify = HasInnerMock(setup);
		}

		public void Deconstruct(out Identifier id, out Setup setup)
		{
			id = this.Id;
			setup = this.Setup;
		}

		private static bool HasInnerMock(Setup setup)
			=> setup is AutoImplementedPropertyGetterSetup 
			|| setup is AutoImplementedPropertySetterSetup
			|| setup is InnerMockSetup;

		public static InvokableSetup NotRegistered => notRegistered;
	}

	internal sealed class SetupCollection
	{
		private readonly List<InvokableSetup> setups;
		private uint overridden;  // bit mask for the first 32 setups flagging those known to be overridden

		public SetupCollection()
		{
			this.setups = new List<InvokableSetup>();
			this.overridden = 0U;
		}

		public void Add(Setup setup)
		{
			lock (this.setups)
			{
				this.setups.Add(new InvokableSetup(this.setups.Count, setup));
			}
		}

		public bool Any(Func<Setup, bool> predicate)
		{
			lock (this.setups)
			{
				return this.setups.Select(r => r.Setup).Any(predicate);
			}
		}

		public void Clear()
		{
			lock (this.setups)
			{
				this.setups.Clear();
				this.overridden = 0U;
			}
		}

		public InvokableSetup FindMatchFor(Invocation invocation)
		{
			// Fast path (no `lock`) when there are no setups:
			if (this.setups.Count == 0)
			{
				return InvokableSetup.NotRegistered;
			}

			InvokableSetup matchingSetup = InvokableSetup.NotRegistered;

			lock (this.setups)
			{
				// Iterating in reverse order because newer setups are more relevant than (i.e. override) older ones
				for (int i = this.setups.Count - 1; i >= 0; --i)
				{
					if (i < 32 && (this.overridden & (1U << i)) != 0) continue;

					var registeredSetup = this.setups[i];

					var (index, setup) = registeredSetup;

					// the following conditions are repetitive, but were written that way to avoid
					// unnecessary expensive calls to `setup.Matches`; cheap tests are run first.
					if (matchingSetup == InvokableSetup.NotRegistered && setup.Matches(invocation))
					{
						matchingSetup = registeredSetup;
						if (setup.Method == invocation.Method)
						{
							break;
						}
					}
					else if (registeredSetup.Setup.Method == invocation.Method && setup.Matches(invocation))
					{
						matchingSetup = registeredSetup;
						break;
					}
				}
			}

			return matchingSetup;
		}

		public IEnumerable<InvokableSetup> GetInnerMockSetups()
		{
			return this.ToArrayLive(setup => setup.ReturnsInnerMock(out _));
		}

		public InvokableSetup[] ToArrayLive(Func<Setup, bool> predicate)
		{
			var matchingSetups = new Stack<InvokableSetup>();
			var visitedSetups = new HashSet<InvocationShape>();

			lock (this.setups)
			{
				// Iterating in reverse order because newer setups are more relevant than (i.e. override) older ones
				for (int i = this.setups.Count - 1; i >= 0; --i)
				{
					if (i < 32 && (this.overridden & (1U << i)) != 0) continue;

					var registeredSetup = this.setups[i];

					var (index, setup) = registeredSetup;

					if (setup.Condition != null)
					{
						continue;
					}

					if (!visitedSetups.Add(setup.Expectation))
					{
						// A setup with the same expression has already been iterated over,
						// meaning that this older setup is an overridden one.
						if (i < 32) this.overridden |= 1U << i;
						continue;
					}

					if (predicate(setup))
					{
						matchingSetups.Push(registeredSetup);
					}
				}
			}

			return matchingSetups.ToArray();
		}
	}
}
