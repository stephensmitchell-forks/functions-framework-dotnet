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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Threading.Tasks;

namespace Google.Cloud.Functions.Framework
{
    /// <summary>
    /// An adapter to implement an HTTP Function based on a Cloud Event Function, with built-in event deserialization.
    /// </summary>
    public sealed class CloudEventAdapter<TData> : IHttpFunction where TData : class
    {
        private readonly ICloudEventFunction<TData> _function;
        private readonly ILogger _logger;

        /// <summary>
        /// Constructs a new instance based on the given Cloud Event Function.
        /// </summary>
        /// <param name="function">The Cloud Event Function to invoke.</param>
        /// <param name="logger">The logger to use to report errors.</param>
        public CloudEventAdapter(ICloudEventFunction<TData> function, ILogger<CloudEventAdapter<TData>> logger)
        {
            _function = Preconditions.CheckNotNull(function, nameof(function));
            _logger = Preconditions.CheckNotNull(logger, nameof(logger));
        }

        /// <summary>
        /// Handles an HTTP request by extracting the Cloud Event from it, deserializing the data, and passing
        /// both the event and the data to the original Cloud Event Function.
        /// The request fails if it does not contain a Cloud Event.
        /// </summary>
        /// <param name="context">The HTTP context containing the request and response.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task HandleAsync(HttpContext context)
        {
            CloudEvent cloudEvent;
            TData data;
            try
            {
                cloudEvent = await CloudEventConverter.ConvertRequest(context.Request);
                data = ConvertData(cloudEvent);
            }
            catch (CloudEventConverter.ConversionException e)
            {
                _logger.LogError(e.Message);
                context.Response.StatusCode = 400;
                return;
            }

            await _function.HandleAsync(cloudEvent, data, context.RequestAborted);
        }

        // Note: static for ease of testing.
        /// <summary>
        /// Converts the data within <paramref name="cloudEvent"/> into the specified type.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent to extract the data from.</param>
        /// <returns>The converted data.</returns>
        internal static TData ConvertData(CloudEvent cloudEvent)
        {
            string text = cloudEvent.Data as string
                ?? throw new CloudEventConverter.ConversionException($"Unable to handle events with a Data property type of '{cloudEvent.Data?.GetType()}'");
            return JsonSerializer.Deserialize<TData>(text);
        }
    }
}
