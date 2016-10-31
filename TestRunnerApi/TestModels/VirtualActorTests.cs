namespace TestRunnerApi.TestModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;

    abstract class VirtualActorTests
    {
        readonly TestState _testState;

        protected VirtualActorTests(TestState testState)
        {
            if (testState == null)
            {
                throw new ArgumentNullException(nameof(testState));
            }

            _testState = testState;
        }

        protected async Task<TestResult> Initialize(Func<Guid, Guid, string, string, ImmutableArray<Guid>, byte[], Task> create)
        {
            var initializationCalls = new Task[_testState.Count];

            var sw = Stopwatch.StartNew();

            for (var i = 0; i != _testState.Count; ++i)
            {
                var friendIndex = _testState.FriendIndex(i);
                initializationCalls[i] = create(_testState.Ids[i], _testState.Ids[friendIndex], _testState.FirstName(i), _testState.LastName(i), _testState.PetIds[i], _testState.ExtraData);
            }

            await Task.WhenAll(initializationCalls);

            sw.Stop();

            return new TestResult(_testState, 1, sw.Elapsed, true);
        }

        protected async Task<TestResult> QueryNames(int iterations, string separator, Func<Guid, Task<string>> queryNames)
        {
            var queryCalls = new Task<bool>[iterations * _testState.Count];

            var sw = Stopwatch.StartNew();

            for (var iteration = 0; iteration != iterations; ++iteration)
            {
                for (var index = 0; index != _testState.Count; ++index)
                {
                    queryCalls[iteration * _testState.Count + index] = CompareNames(queryNames(_testState.Ids[index]), _testState.FirstName(index) + separator + _testState.LastName(index));
                }
            }

            var results = await Task.WhenAll(queryCalls);
            sw.Stop();

            return new TestResult(_testState, iterations, sw.Elapsed, results.All(x => x));
        }

        protected async Task<TestResult> QueryPetNames(int iterations, Func<Guid, Task<IEnumerable<string>>> queryNames)
        {
            var queryCalls = new Task<bool>[iterations * _testState.Count];

            var sw = Stopwatch.StartNew();

            for (var iteration = 0; iteration != iterations; ++iteration)
            {
                for (var grainIndex = 0; grainIndex != _testState.Count; ++grainIndex)
                {
                    queryCalls[iteration * _testState.Count + grainIndex] = CompareNames(queryNames(_testState.Ids[grainIndex]), _testState.PetNames(grainIndex));
                }
            }

            var results = await Task.WhenAll(queryCalls);
            sw.Stop();

            return new TestResult(_testState, iterations, sw.Elapsed, results.All(x => x));
        }

        protected async Task<TestResult> QueryFriendNames(int iterations, int depth, string separator, Func<Guid, Task<string>> queryNames)
        {
            var queryCalls = new Task<bool>[iterations * _testState.Count];

            var sw = Stopwatch.StartNew();

            for (var iteration = 0; iteration != iterations; ++iteration)
            {
                for (var index = 0; index < _testState.Count; index += depth + 1)
                {
                    var friendNames = _testState.FriendNames(index, depth);

                    queryCalls[iteration * _testState.Count + index] =
                        CompareNames(
                            queryNames(_testState.Ids[index]),
                            string.Join(separator, friendNames));
                }
            }

            var results = await Task.WhenAll(queryCalls.Where(t => t != null));
            sw.Stop();

            return new TestResult(_testState, iterations, sw.Elapsed, results.All(x => x));
        }

        protected async Task<TestResult> UpdateNames(int iterations, ImmutableArray<ImmutableArray<string>> newNames, Func<Guid, string, Task<string>> updateName)
        {
            var results = new List<bool>(iterations * _testState.Count);

            var sw = Stopwatch.StartNew();

            for (var iteration = 0; iteration != iterations; ++iteration)
            {
                var updateCalls = new Task<bool>[_testState.Count];

                for (var index = 0; index < _testState.Count; ++index)
                {
                    updateCalls[index] =
                        CompareNames(
                            updateName(_testState.Ids[index], newNames[iteration][index]),
                            newNames[iteration][index]);
                }

                // have to await each iteration or else the write/read pairs could get interleaved between iterations
                results.AddRange(await Task.WhenAll(updateCalls));
            }
            
            sw.Stop();

            return new TestResult(_testState, iterations, sw.Elapsed, results.All(x => x));
        }

        static async Task<bool> CompareNames(Task<string> remoteName, string expected)
        {
            var remote = await remoteName;
            return remote == expected;
        }

        static async Task<bool> CompareNames(Task<IEnumerable<string>> remotePetNames, IEnumerable<string> expectedNames) => !(await remotePetNames).Except(expectedNames).Any();
    }
}
