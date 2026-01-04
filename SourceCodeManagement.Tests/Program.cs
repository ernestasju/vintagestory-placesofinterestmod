#if USE_TEST_RUNNER == false
using SourceCodeManagement;
using System;

//throw new Exception("Do tests in your own place.");

var session = new TestSession();
Console.WriteLine(session.Client.Chat.Commands.CopyPlaces(100, "park,nature"));

[ClassWithNamespaces]
partial class TestSession
{
    public partial class ClientNamespace
    {
        private object CreateClientSide()
        {
            return default!;
        }

        public partial class ChatNamespace
        {
            public partial class CommandsNamespace
            {
                public string CopyPlaces(int radius, string tags)
                {
                    object clientSide = Client.CreateClientSide();
                    return "Handled CopyPlaces command";
                }

                public string PastePlaces(string existingPlaceAction) =>
                    "Handled PastePlaces command";
            }
        }
    }
}
#else
TestRunner.RunTests();
SourceCodeManagement.Tests.SnapshotPrinter.PrintAllTypesAndProperties();
#endif
