// Copyright © 2012-2020 VLINGO LABS. All rights reserved.
//
// This Source Code Form is subject to the terms of the
// Mozilla Public License, v. 2.0. If a copy of the MPL
// was not distributed with this file, You can obtain
// one at https://mozilla.org/MPL/2.0/.

using System;
using Vlingo.Common;
using Vlingo.Symbio.Store.Object;

namespace Vlingo.Symbio.Tests.Store.Object.InMemory
{
    public class Person : StateObject, IComparable<Person>
    {
        private static AtomicLong _identityGenerator = new AtomicLong(0);

        public Person(string name, int age, long persistenceId = -1L) : base(persistenceId > -1L ? persistenceId : _identityGenerator.IncrementAndGet())
        {
            Name = name;
            Age = age;
        }

        public int Age { get; }
        
        public string Name { get; }

        public int CompareTo(Person other)
        {
            if (PersistenceId == other.PersistenceId)
            {
                return 0;
            }

            if (PersistenceId < other.PersistenceId)
            {
                return -1;
            }

            return 1;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || obj.GetType() != GetType())
            {
                return false;
            }

            if (this == obj)
            {
                return true;
            }

            var otherPerson = (Person) obj;

            return PersistenceId == otherPerson.PersistenceId;
        }

        public override int GetHashCode() => 31 * Name.GetHashCode() * Age;

        public override string ToString() => $"Person[persistenceId={PersistenceId} name={Name} age={Age}]";
    }
}