﻿// Copyright 2020, Google LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using CloudNative.CloudEvents;
using Google.Cloud.Functions.Framework.GcfEvents;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Google.Cloud.Functions.Framework.Tests.GcfEvents
{
    public class GcfConvertersTest
    {
        // Checks a basic mapping for each event source
        [Theory]
        [InlineData("storage.json", "com.google.cloud.storage.object.finalize.v0", "//storage.googleapis.com/projects/_/buckets/some-bucket", "objects/folder/Test.cs")]
        [InlineData("legacy_storage_change.json", "com.google.cloud.storage.object.change.v0", "//storage.googleapis.com/projects/_/buckets/sample-bucket", "objects/MyFile")]
        [InlineData("firestore_simple.json", "com.google.cloud.firestore.document.write.v0", "//firestore.googleapis.com/projects/project-id/databases/(default)/documents/gcf-test/2Vm2mI1d0wIaK2Waj5to", null)]
        [InlineData("pubsub_text.json", "com.google.cloud.pubsub.topic.publish.v0", "//pubsub.googleapis.com/projects/sample-project/topics/gcf-test", null)]
        [InlineData("legacy_pubsub.json", "com.google.cloud.pubsub.topic.publish.v0", "//pubsub.googleapis.com/projects/sample-project/topics/gcf-test", null)]
        [InlineData("firebase-db1.json", "com.google.cloud.firebase.database.write.v0", "//firebase.googleapis.com/projects/_/instances/my-project-id/refs/gcf-test/xyz", null)]
        [InlineData("firebase-auth1.json", "com.google.cloud.firebase.auth.user.create.v0", "//firebase.googleapis.com/projects/my-project-id", null)]
        [InlineData("firebase-auth2.json", "com.google.cloud.firebase.auth.user.delete.v0", "//firebase.googleapis.com/projects/my-project-id", null)]
        public async Task ConvertGcfEvent(string resourceName, string expectedType, string expectedSource, string expectedSubject)
        {
            var context = GcfEventResources.CreateHttpContext(resourceName);
            var cloudEvent = await GcfConverters.ConvertGcfEventToCloudEvent(context.Request);
            Assert.Equal(expectedType, cloudEvent.Type);
            Assert.Equal(new Uri(expectedSource), cloudEvent.Source);
            Assert.Equal(expectedSubject, cloudEvent.Subject);
        }

        // Checks everything we know about a single event
        [Fact]
        public async Task CheckAllProperties()
        {
            var context = GcfEventResources.CreateHttpContext("storage.json");
            var cloudEvent = await GcfConverters.ConvertGcfEventToCloudEvent(context.Request);
            Assert.Equal("application/json", cloudEvent.DataContentType.MediaType);
            Assert.Equal("1147091835525187", cloudEvent.Id);
            Assert.Equal("com.google.cloud.storage.object.finalize.v0", cloudEvent.Type);
            Assert.Equal(new DateTime(2020, 4, 23, 7, 38, 57, 772), cloudEvent.Time);
            Assert.Equal(new Uri("//storage.googleapis.com/projects/_/buckets/some-bucket"), cloudEvent.Source);
            Assert.Equal("objects/folder/Test.cs", cloudEvent.Subject);
            Assert.Equal(CloudEventsSpecVersion.V1_0, cloudEvent.SpecVersion);
            Assert.Null(cloudEvent.DataSchema);
            Assert.IsType<string>(cloudEvent.Data);
        }

        // Minimal valid JSON, so all the subsequent invalid tests can be "this JSON with something removed"
        [Fact]
        public async Task MinimalValidEvent()
        {
            string json = "{'data':{}, 'context':{'eventId':'xyz', 'eventType': 'google.pubsub.topic.publish', 'resource':{'service': 'svc', 'name': 'resname'}}}";
            var cloudEvent = await ConvertJson(json);
            Assert.Equal("xyz", cloudEvent.Id);
            Assert.Equal(new Uri("//svc/resname"), cloudEvent.Source);
        }

        [Fact]
        public Task InvalidRequest_UnableToDeserialize() =>
            AssertInvalidRequest("{INVALIDJSON 'data':{}, 'context':{'eventId':'xyz', 'eventType': 'google.pubsub.topic.publish', 'resource':{'service': 'svc', 'name': 'resname'}}}");

        [Fact]
        public Task InvalidRequest_NoData() =>
            AssertInvalidRequest("{'context':{'eventId':'xyz', 'eventType': 'google.pubsub.topic.publish', 'resource':{'service': 'svc', 'name': 'resname'}}}");

        [Fact]
        public Task InvalidRequest_NoId() =>
            AssertInvalidRequest("{'data':{}, 'context':{'eventType': 'google.pubsub.topic.publish', 'resource':{'service': 'svc', 'name': 'resname'}}}");

        [Fact]
        public Task InvalidRequest_NoType() =>
            AssertInvalidRequest("{'data':{}, 'context':{'eventId':'xyz', 'resource':{'service': 'svc', 'name': 'resname'}}}");

        [Fact]
        public Task InvalidRequest_NoResourceName() =>
            AssertInvalidRequest("{'data':{}, 'context':{'eventId':'xyz', 'eventType': 'google.pubsub.topic.publish', 'resource':{'service': 'svc'}}}");

        private static async Task AssertInvalidRequest(string json, string? contentType = null) =>
            await Assert.ThrowsAsync<CloudEventConverter.ConversionException>(() => ConvertJson(json, contentType));

        private static async Task<CloudEvent> ConvertJson(string json, string? contentType = null)
        {
            var request = new DefaultHttpContext
            {
                Request =
                {
                    Body = new MemoryStream(Encoding.UTF8.GetBytes(json.Replace('\'', '"'))),
                    ContentType = contentType ?? "application/json"
                }
            }.Request;
            return await GcfConverters.ConvertGcfEventToCloudEvent(request);
        }
    }
}
