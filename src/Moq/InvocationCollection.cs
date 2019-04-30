// Copyright (c) 2007, Clarius Consulting, Manas Technology Solutions, InSTEDD.
// All rights reserved. Licensed under the BSD 3-Clause License; see License.txt.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Moq
{
	internal sealed class InvocationCollection : IInvocationList
	{
		internal struct TimestampIdentifier : IEquatable<TimestampIdentifier>
		{
			private readonly InvokableSetup.Identifier identifier;

			private readonly int timestamp;

			public TimestampIdentifier(InvokableSetup.Identifier identifier, int version)
			{
				this.identifier = identifier;
				this.timestamp = version;
			}

			public int GetHashCode(TimestampIdentifier obj)
			{
				return identifier.Id.GetHashCode();
			}

			public bool Equals(TimestampIdentifier other)
			{
				return this.identifier.Id == other.identifier.Id && this.timestamp <= other.timestamp;
			}
		}

		internal class InvocationContext : IDisposable
		{
			private IReadOnlyDictionary<TimestampIdentifier, Invocation> lookup;
			private readonly int version;
			private readonly object syncRoot;

			internal InvocationContext(IReadOnlyDictionary<TimestampIdentifier, Invocation> lookup, int version, object syncRoot)
			{
				this.lookup = lookup;
				this.version = version;
				this.syncRoot = syncRoot;
			}

			public bool IsMatchedByInvocation(InvokableSetup setup, Func<InvokableSetup, bool> dontVerify)
			{
				if (dontVerify(setup))
				{
					return true;
				}

				lock (syncRoot)
				{
					if (this.lookup != null && this.lookup.TryGetValue(new TimestampIdentifier(setup.Id, this.version), out Invocation invocation))
					{
						invocation.MarkAsVerified();
						return true;
					}
				}

				return false;
			}

			public void Dispose()
			{
				this.lookup = null;
			}
		}

		private Invocation[] invocations;
		private Dictionary<TimestampIdentifier, Invocation> matchedInvocations;

		private int capacity = 0;
		private int count = 0;
		private int timestamp = 0;

		private readonly object invocationsLock = new object();

		public int Count
		{
			get
			{
				lock (this.invocationsLock)
				{
					return count;
				}
			}
		}

		public IInvocation this[int index]
		{
			get
			{
				lock (this.invocationsLock)
				{
					if (this.count <= index || index < 0)
					{
						throw new IndexOutOfRangeException();
					}

					return this.invocations[index];
				}
			}
		}

		public void Add(Invocation invocation)
		{
			lock (this.invocationsLock)
			{
				EnsureCapacity();

				this.invocations[this.count] = invocation;
				this.count++;
			}
		}

		public void RecordMatchedInvocation(InvokableSetup.Identifier identifier, Invocation invocation)
		{
			lock(this.invocationsLock)
			{
				this.matchedInvocations[new TimestampIdentifier(identifier, ++this.timestamp)] = invocation;
			}
		}

		public void Clear()
		{
			lock (this.invocationsLock)
			{
				// Replace the collection so readers with a reference to the old collection aren't interrupted
				this.invocations = null;
				this.matchedInvocations = null;
				this.count = 0;
				this.capacity = 0;
			}
		}

		public Invocation[] ToArray()
		{
			lock (this.invocationsLock)
			{
				if (this.count == 0)
				{
					return new Invocation[0];
				}

				var result = new Invocation[this.count];

				Array.Copy(this.invocations, result, this.count);

				return result;
			}
		}

		public Invocation[] ToArray(Func<Invocation, bool> predicate)
		{
			lock (this.invocationsLock)
			{
				if (this.count == 0)
				{
					return new Invocation[0];
				}
				
				var result = new List<Invocation>(this.count);

				for (var i = 0; i < this.count; i++)
				{
					var invocation = this.invocations[i];
					if (predicate(invocation))
					{
						result.Add(invocation);
					}
				}

				return result.ToArray();
			}
		}

		public IEnumerator<IInvocation> GetEnumerator()
		{
			// Take local copies of collection and count so they are isolated from changes by other threads.
			Invocation[] collection;
			int count;

			lock (this.invocationsLock)
			{
				collection = this.invocations;
				count = this.count;
			}

			for (var i = 0; i < count; i++)
			{
				yield return collection[i];
			}
		}

		internal InvocationContext AsInvocationContext()
		{
			lock (this.invocationsLock)
			{
				return new InvocationContext(this.matchedInvocations, this.timestamp, this.invocationsLock);
			}
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		private void EnsureCapacity()
		{
			if (this.count == this.capacity)
			{
				var targetCapacity = this.capacity == 0 ? DefaultCapacity() : (this.capacity * 2);
				Array.Resize(ref this.invocations, targetCapacity);
				this.capacity = targetCapacity;
			}
		}

		private int DefaultCapacity()
		{
			this.matchedInvocations = new Dictionary<TimestampIdentifier, Invocation>();
			return 4;
		}
	}
}
