// This file is part of Project Hybrasyl.
// 
// This program is free software; you can redistribute it and/or modify
// it under the terms of the Affero General Public License as published by
// the Free Software Foundation, version 3.
// 
// This program is distributed in the hope that it will be useful, but
// without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
// for more details.
// 
// You should have received a copy of the Affero General Public License along
// with this program. If not, see <http://www.gnu.org/licenses/>.
// 
// (C) 2020-2023 ERISCO, LLC
// 
// For contributors and individual authors please refer to CONTRIBUTORS.MD.

using System.Collections.Generic;
using Hybrasyl.Xml.Objects;

namespace Hybrasyl.Plugins;

public interface IPluginResponse
{
    public bool Success { get; set; }
    public string PluginResponse { get; set; }
}

public interface IMessagePluginResponse : IPluginResponse
{
    public Message Message { get; set; }
    public bool Transformed { get; }
}

public interface IHandlerConfiguration
{
    public void LoadXmlConfig(List<PluginConfig> config);
    public bool StoreValue(string key, string value);
    public bool TryGetValue(string key, out string value);
}

/// <summary>
///     A base interface for message handlers.
/// </summary>
public interface IMessageHandler
{
    public bool Disabled { get; set; }
    public bool Passthrough { get; set; }
    public bool Initialize(IHandlerConfiguration config);
    public void SetTargets(List<string> targets);
    public bool WillHandle(string target);
}

public interface IProcessingMessageHandler : IMessageHandler
{
    public IMessagePluginResponse Process(Message inbound);
}