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

namespace Hybrasyl.Interfaces;

// This exists to identify all usages of WorldStateData, to ensure we are not cross-mixing types between
// WorldStateData and XMLManager / WorldStoreData, and to reduce / eliminate bugs from extracting all XML
// type access out to the new XM library. These types of errors will be caught at the compiler level as
// WorldStateData now has where clauses only allowing types implementing this interface to be used with it.
//
// It may be used for more in the future to make WorldStateData similar in patterns to XMLManager.
public interface IStateStorable { }