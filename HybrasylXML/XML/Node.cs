/*
 * This file is part of Project Hybrasyl.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the Affero General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful, but
 * without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
 * for more details.
 *
 * You should have received a copy of the Affero General Public License along
 * with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * (C) 2016 Project Hybrasyl (info@hybrasyl.com)
 *
 */
 
using System;
using System.Linq;
using System.Xml.Linq;

namespace Hybrasyl.XML
{
    public static class Node
    {
        public static string Replace(string nodeName, string source, string contents)
        {
            try
            {
                var xContents = XElement.Parse(contents);

                var xTraverse = XElement.Parse(source);
                XElement node = xTraverse.Descendants(nodeName).First();
                node.ReplaceWith(xContents);
                return xTraverse.ToString();
            }
            catch (Exception e)
            {
                //need exception handling
            }
            return source;
        }
    }
}