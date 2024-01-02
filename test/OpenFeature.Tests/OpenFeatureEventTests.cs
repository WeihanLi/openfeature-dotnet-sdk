using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OpenFeature.Constant;
using OpenFeature.Model;
using OpenFeature.Tests.Internal;
using Xunit;

namespace OpenFeature.Tests
{
    [SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task")]
    public class OpenFeatureEventTests : ClearOpenFeatureInstanceFixture
    {
        [Fact]
        public async Task Event_Executor_Should_Propagate_Events_ToGlobal_Handler()
        {
            var eventHandler = Substitute.For<EventHandlerDelegate>();

            eventHandler.Invoke(Arg.Any<ProviderEventPayload>());

            var eventExecutor = new EventExecutor();

            eventExecutor.AddApiLevelHandler(ProviderEventTypes.PROVIDER_READY, eventHandler);

            var eventPayload = new Event { EventPayload = new ProviderEventPayload { Type = ProviderEventTypes.PROVIDER_READY }};
            eventExecutor.eventChannel.Writer.TryWrite(eventPayload);

            eventHandler.Received().Invoke(Arg.Is<ProviderEventPayload>(payload => payload.Type == ProviderEventTypes.PROVIDER_READY));

            // shut down the event executor
            await eventExecutor.SignalShutdownAsync();

            // the next event should not be propagated to the event handler
            var newEventPayload = new ProviderEventPayload { Type = ProviderEventTypes.PROVIDER_STALE };

            eventExecutor.eventChannel.Writer.TryWrite(newEventPayload);

            eventHandler.DidNotReceive().Invoke(newEventPayload);

            eventHandler.DidNotReceive().Invoke(Arg.Is<ProviderEventPayload>(payload => payload.Type == ProviderEventTypes.PROVIDER_STALE));
        }

        [Fact]
        public async Task API_Level_Event_Handlers_Should_Be_Registered()
        {
            var eventHandler = Substitute.For<EventHandlerDelegate>();

            eventHandler.Invoke(Arg.Any<ProviderEventPayload>());

            Api.Instance.AddHandler(ProviderEventTypes.PROVIDER_READY, eventHandler);

            var testProvider = new TestProvider();
            await Api.Instance.SetProvider(testProvider);

            eventHandler.Received().Invoke(Arg.Is<ProviderEventPayload>(payload => payload.ProviderName == testProvider.GetMetadata().Name));
        }

        [Fact]
        public async Task API_Level_Event_Handlers_Should_Be_Informed_About_Ready_State_After_Registering()
        {
            var eventHandler = Substitute.For<EventHandlerDelegate>();

            var testProvider = new TestProvider();
            await Api.Instance.SetProvider(testProvider);

            Api.Instance.AddHandler(ProviderEventTypes.PROVIDER_READY, eventHandler);

            Thread.Sleep(1000);
            eventHandler.Received().Invoke(Arg.Is<ProviderEventPayload>(payload => payload.ProviderName == testProvider.GetMetadata().Name));
        }

        [Fact]
        public async Task API_Level_Event_Handlers_Should_Be_Exchangeable()
        {
            var eventHandler = Substitute.For<EventHandlerDelegate>();

            Api.Instance.AddHandler(ProviderEventTypes.PROVIDER_READY, eventHandler);

            var testProvider = new TestProvider();
            await Api.Instance.SetProvider(testProvider);

            var newTestProvider = new TestProvider();
            await Api.Instance.SetProvider(newTestProvider);

            Thread.Sleep(1000);
            eventHandler.Received(2).Invoke(Arg.Is<ProviderEventPayload>(payload => payload.ProviderName == testProvider.GetMetadata().Name));
        }

        [Fact]
        public async Task API_Level_Event_Handlers_Should_Be_Removable()
        {
            var eventHandler = Substitute.For<EventHandlerDelegate>();

            Api.Instance.AddHandler(ProviderEventTypes.PROVIDER_READY, eventHandler);

            var testProvider = new TestProvider();
            await Api.Instance.SetProvider(testProvider);

            Thread.Sleep(1000);

            eventHandler.Received().Invoke(Arg.Is<ProviderEventPayload>(payload => payload.ProviderName == testProvider.GetMetadata().Name));

            Api.Instance.RemoveHandler(ProviderEventTypes.PROVIDER_READY, eventHandler);

            var newTestProvider = new TestProvider();
            await Api.Instance.SetProvider(newTestProvider);

            // now we should not receive any event since we have removed the event handler
            Received.InOrder(async () => {});
        }

        [Fact]
        public async Task Client_Level_Event_Handlers_Should_Be_Registered()
        {
            var fixture = new Fixture();
            var eventHandler = Substitute.For<EventHandlerDelegate>();

            eventHandler.Invoke(Arg.Any<ProviderEventPayload>());

            var myClient = Api.Instance.GetClient(fixture.Create<string>());

            var testProvider = new TestProvider();
            await Api.Instance.SetProvider(myClient.GetMetadata().Name, testProvider);

            myClient.AddHandler(ProviderEventTypes.PROVIDER_READY, eventHandler);

            eventHandler.Received().Invoke(Arg.Is<ProviderEventPayload>(payload => payload.ProviderName == testProvider.GetMetadata().Name));
        }

        [Fact]
        public async Task Client_Level_Event_Handlers_Should_Be_Informed_About_Ready_State_After_Registering()
        {
            var fixture = new Fixture();
            var eventHandler = Substitute.For<EventHandlerDelegate>();

            eventHandler.Invoke(Arg.Any<ProviderEventPayload>());

            var myClient = Api.Instance.GetClient(fixture.Create<string>());

            var testProvider = new TestProvider();
            await Api.Instance.SetProvider(myClient.GetMetadata().Name, testProvider);

            // add the event handler after the provider has already transitioned into the ready state
            myClient.AddHandler(ProviderEventTypes.PROVIDER_READY, eventHandler);

            eventHandler.Received().Invoke(Arg.Is<ProviderEventPayload>(payload => payload.ProviderName == testProvider.GetMetadata().Name));
        }

        [Fact]
        public async Task Client_Level_Event_Handlers_Should_Be_Removable()
        {
            var fixture = new Fixture();

            var eventHandler = Substitute.For<EventHandlerDelegate>();

            var myClient = Api.Instance.GetClient(fixture.Create<string>());

            myClient.AddHandler(ProviderEventTypes.PROVIDER_READY, eventHandler);

            var testProvider = new TestProvider();
            await Api.Instance.SetProvider(myClient.GetMetadata().Name, testProvider);

            // wait for the first event to be received
            Thread.Sleep(1000);
            myClient.RemoveHandler(ProviderEventTypes.PROVIDER_READY, eventHandler);

            var newTestProvider = new TestProvider();
            await Api.Instance.SetProvider(myClient.GetMetadata().Name, newTestProvider);

            // wait a bit and make sure we only have received the first event, but nothing after removing the event handler
            Thread.Sleep(1000);
            eventHandler.Received(1).Invoke(Arg.Is<ProviderEventPayload>(payload => payload.ProviderName == testProvider.GetMetadata().Name));
        }
    }
}