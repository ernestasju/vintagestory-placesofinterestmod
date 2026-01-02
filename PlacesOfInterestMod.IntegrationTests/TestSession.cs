//using Moq;
//using PlacesOfInterestMod;

//namespace PlacesOfInterestMod.IntegrationTests;

//[ClassWithNamespaces]
//partial class TestSession
//{
//    // do not remove: this is used to check if generator does not create constructor if one already exists
//    internal TestSession()
//    {
//        Client = new(this);
//    }

//#pragma warning disable SA1205 // Partial elements should declare access
//    public partial class ClientNamespace
//    {
//        internal Action<Mock<IClientSide>>? SetupClientSideMock { get; set; }

//        private IClientSide CreateClientSide()
//        {
//            Mock<IClientSide> clientSideMock = new();
//            SetupClientSideMock?.Invoke(clientSideMock);
//            return clientSideMock.Object;
//        }

//        sealed partial class ChatNamespace
//        {
//            // do not remove: this is used to check if generator does not create constructor if one already exists
//            internal ChatNamespace(ClientNamespace parent)
//            {
//                _parent = parent;

//                Commands = new(this);
//            }

//            private partial class CommandsNamespace
//            {
//                internal LocalizedTextCommandResult CopyPlaces(int radius, string tags) =>
//                    ClientSide.HandleChatCommandCopyInterestingPlaces(Client.CreateClientSide(), radius, tags);

//                internal LocalizedTextCommandResult PastePlaces(ExistingPlaceAction existingPlaceAction) =>
//                    ClientSide.HandleChatCommandPasteInterestingPlaces(Client.CreateClientSide(), existingPlaceAction);
//            }
//        }
//    }
//#pragma warning restore SA1205 // Partial elements should declare access
//}
