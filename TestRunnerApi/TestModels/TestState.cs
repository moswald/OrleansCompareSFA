namespace TestRunnerApi.TestModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;

    public class TestState
    {
        public int Count { get; set; }
        public ImmutableArray<Guid> Ids { get; set; }
        public ImmutableArray<string> Names { get; set; }
        public ImmutableArray<ImmutableArray<Guid>> PetIds { get; set; }
        public byte[] ExtraData { get; set; }

        public ImmutableArray<double> CalculatorTestValues { get; set; }
        public double ExpectedSum { get; set; }

        public string FirstName(int index) => Names[index * 2];
        public string LastName(int index) => Names[index * 2 + 1];
        public IEnumerable<string> PetNames(int index) => PetIds[index].Any() ? PetIds[index].Select(g => g.ToString()).ToArray() : Array.Empty<string>();
        public int FriendIndex(int index) => index + 1 == Count ? 0 : index + 1;

        public IEnumerable<string> FriendNames(int index, int count)
        {
            var names = new List<string>();

            for (var i = 0; i != count + 1; ++i)
            {
                names.Add(FirstName(index));
                index = FriendIndex(index);
            }

            return names;
        }
    }
}
