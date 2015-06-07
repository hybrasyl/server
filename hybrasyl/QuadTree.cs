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
 * (C) 2013 Project Hybrasyl (info@hybrasyl.com)
 *
 * Authors:   Justin Baugh  <baughj@hybrasyl.com>
 *            Kyle Speck    <kojasou@hybrasyl.com>
 */

using System.Collections;
using System.Collections.Generic;
using System.Drawing;

namespace C3
{
    /// <summary>
    /// Interface to define Rect, so that QuadTree knows how to store the object.
    /// </summary>
    public interface IQuadStorable
    {
        /// <summary>
        /// The rectangle that defines the object's boundaries.
        /// </summary>
        Rectangle Rect { get; }

        /// <summary>
        /// This should return True if the object has moved during the last update, false otherwise
        /// </summary>
        bool HasMoved { get; }
    }

    /// <summary>
    /// Used internally to attach an Owner to each object stored in the QuadTree
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class QuadTreeObject<T>
        where T: IQuadStorable
    {
        /// <summary>
        /// The wrapped data value
        /// </summary>
        public T Data { get;
            private set; }

        /// <summary>
        /// The QuadTreeNode that owns this object
        /// </summary>
        internal QuadTreeNode<T> Owner { get;
            set; }

        /// <summary>
        /// Wraps the data value
        /// </summary>
        /// <param name="data">The data value to wrap</param>
        public QuadTreeObject(T data)
        {
            Data = data;
        }
    }

    /// <summary>
    /// A QuadTree Object that provides fast and efficient storage of objects in a world space.
    /// </summary>
    /// <typeparam name="T">Any object implementing IQuadStorable.</typeparam>
    public class QuadTree<T> : ICollection<T>
        where T: IQuadStorable
    {
        private readonly Dictionary<T, QuadTreeObject<T>> wrappedDictionary = new Dictionary<T, QuadTreeObject<T>>();

        private readonly QuadTreeNode<T> quadTreeRoot;

        /// <summary>
        /// Creates a QuadTree for the specified area.
        /// </summary>
        /// <param name="rect">The area this QuadTree object will encompass.</param>
        public QuadTree(Rectangle rect)
        {
            quadTreeRoot = new QuadTreeNode<T>(rect);
        }


        /// <summary>
        /// Creates a QuadTree for the specified area.
        /// </summary>
        /// <param name="x">The top-left position of the area rectangle.</param>
        /// <param name="y">The top-right position of the area rectangle.</param>
        /// <param name="width">The width of the area rectangle.</param>
        /// <param name="height">The height of the area rectangle.</param>
        public QuadTree(int x, int y, int width, int height)
        {
            quadTreeRoot = new QuadTreeNode<T>(new Rectangle(x, y, width, height));
        }

        /// <summary>
        /// Gets the rectangle that bounds this QuadTree
        /// </summary>
        public Rectangle QuadRect
        {
            get
            {
                return quadTreeRoot.QuadRect;
            }
        }

        /// <summary>
        /// Get the objects in this tree that intersect with the specified rectangle.
        /// </summary>
        /// <param name="rect">The rectangle to find objects in.</param>
        public List<T> GetObjects(Rectangle rect)
        {
            return quadTreeRoot.GetObjects(rect);
        }


        /// <summary>
        /// Get the objects in this tree that intersect with the specified rectangle.
        /// </summary>
        /// <param name="rect">The rectangle to find objects in.</param>
        /// <param name="results">A reference to a list that will be populated with the results.</param>
        public void GetObjects(Rectangle rect, ref List<T> results)
        {
            quadTreeRoot.GetObjects(rect, ref results);
        }


        /// <summary>
        /// Get all objects in this Quad, and it's children.
        /// </summary>
        public List<T> GetAllObjects()
        {
            return new List<T>(wrappedDictionary.Keys);
        }


        /// <summary>
        /// Moves the object in the tree
        /// </summary>
        /// <param name="item">The item that has moved</param>
        public bool Move(T item)
        {
            if (Contains(item))
            {
                quadTreeRoot.Move(wrappedDictionary[item]);
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Adds an item to the <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </summary>
        /// <param name="item">The object to add to the <see cref="T:System.Collections.Generic.ICollection`1"/>.</param>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only.</exception>
        public void Add(T item)
        {
            var wrappedObject = new QuadTreeObject<T>(item);
            wrappedDictionary.Add(item, wrappedObject);
            quadTreeRoot.Insert(wrappedObject);
        }


        /// <summary>
        /// Removes all items from the <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </summary>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only. </exception>
        public void Clear()
        {
            wrappedDictionary.Clear();
            quadTreeRoot.Clear();
        }


        /// <summary>
        /// Determines whether the <see cref="T:System.Collections.Generic.ICollection`1"/> contains a specific value.
        /// </summary>
        /// <returns>
        /// true if <paramref name="item"/> is found in the <see cref="T:System.Collections.Generic.ICollection`1"/>; otherwise, false.
        /// </returns>
        /// <param name="item">The object to locate in the <see cref="T:System.Collections.Generic.ICollection`1"/>.</param>
        public bool Contains(T item)
        {
            return wrappedDictionary.ContainsKey(item);
        }


        /// <summary>
        /// Copies the elements of the <see cref="T:System.Collections.Generic.ICollection`1"/> to an <see cref="T:System.Array"/>, starting at a particular <see cref="T:System.Array"/> index.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="T:System.Array"/> that is the destination of the elements copied from <see cref="T:System.Collections.Generic.ICollection`1"/>. The <see cref="T:System.Array"/> must have zero-based indexing.</param>
        /// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="array"/> is null.</exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is less than 0.</exception>
        /// <exception cref="T:System.ArgumentException"><paramref name="array"/> is multidimensional.-or-<paramref name="arrayIndex"/> is equal to or greater than the length of <paramref name="array"/>.-or-The number of elements in the source <see cref="T:System.Collections.Generic.ICollection`1"/> is greater than the available space from <paramref name="arrayIndex"/> to the end of the destination <paramref name="array"/>.-or-Type <paramref name="T"/> cannot be cast automatically to the type of the destination <paramref name="array"/>.</exception>
        public void CopyTo(T[] array, int arrayIndex)
        {
            wrappedDictionary.Keys.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Gets the number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </summary>
        /// <returns>
        /// The number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </returns>
        public int Count
        {
            get
            {
                return wrappedDictionary.Count;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only.
        /// </summary>
        /// <returns>
        /// true if the <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only; otherwise, false.
        /// </returns>
        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Removes the first occurrence of a specific object from the <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </summary>
        /// <returns>
        /// true if <paramref name="item"/> was successfully removed from the <see cref="T:System.Collections.Generic.ICollection`1"/>; otherwise, false. This method also returns false if <paramref name="item"/> is not found in the original <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </returns>
        /// <param name="item">The object to remove from the <see cref="T:System.Collections.Generic.ICollection`1"/>.</param>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only.</exception>
        public bool Remove(T item)
        {
            if (Contains(item))
            {
                quadTreeRoot.Delete(wrappedDictionary[item], true);
                wrappedDictionary.Remove(item);
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.IEnumerator`1"/> that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public IEnumerator<T> GetEnumerator()
        {
            return wrappedDictionary.Keys.GetEnumerator();
        }


        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }


        /// <summary>
        /// The top left child for this QuadTree, only usable in debug mode
        /// </summary>
        public QuadTreeNode<T> RootQuad
        {
            get
            {
                return quadTreeRoot;
            }
        }
    }

    /// <summary>
    /// A QuadTree Object that provides fast and efficient storage of objects in a world space.
    /// </summary>
    /// <typeparam name="T">Any object implementing IQuadStorable.</typeparam>
    public class QuadTreeNode<T>
        where T: IQuadStorable
    {
        private const int MaxObjectsPerNode = 2;

        private List<QuadTreeObject<T>> objects = null;
        private Rectangle rect;

        private QuadTreeNode<T> parent = null;

        private QuadTreeNode<T> childTL = null;
        private QuadTreeNode<T> childTR = null;
        private QuadTreeNode<T> childBL = null;
        private QuadTreeNode<T> childBR = null;

        /// <summary>
        /// The area this QuadTree represents.
        /// </summary>
        public Rectangle QuadRect
        {
            get
            {
                return rect;
            }
        }

        /// <summary>
        /// The top left child for this QuadTree
        /// </summary>
        public QuadTreeNode<T> TopLeftChild
        {
            get
            {
                return childTL;
            }
        }

        /// <summary>
        /// The top right child for this QuadTree
        /// </summary>
        public QuadTreeNode<T> TopRightChild
        {
            get
            {
                return childTR;
            }
        }

        /// <summary>
        /// The bottom left child for this QuadTree
        /// </summary>
        public QuadTreeNode<T> BottomLeftChild
        {
            get
            {
                return childBL;
            }
        }

        /// <summary>
        /// The bottom right child for this QuadTree
        /// </summary>
        public QuadTreeNode<T> BottomRightChild
        {
            get
            {
                return childBR;
            }
        }

        /// <summary>
        /// This QuadTree's parent
        /// </summary>
        public QuadTreeNode<T> Parent
        {
            get
            {
                return parent;
            }
        }

        /// <summary>
        /// The objects contained in this QuadTree at it's level (ie, excludes children)
        /// </summary>
        internal List<QuadTreeObject<T>> Objects
        {
            get
            {
                return objects;
            }
        }

        /// <summary>
        /// How many total objects are contained within this QuadTree (ie, includes children)
        /// </summary>
        public int Count
        {
            get
            {
                return ObjectCount();
            }
        }

        /// <summary>
        /// Returns true if this is a empty leaf node
        /// </summary>
        public bool IsEmptyLeaf
        {
            get
            {
                return Count == 0 && childTL == null;
            }
        }

        /// <summary>
        /// Creates a QuadTree for the specified area.
        /// </summary>
        /// <param name="rect">The area this QuadTree object will encompass.</param>
        public QuadTreeNode(Rectangle rect)
        {
            this.rect = rect;
        }


        /// <summary>
        /// Creates a QuadTree for the specified area.
        /// </summary>
        /// <param name="x">The top-left position of the area rectangle.</param>
        /// <param name="y">The top-right position of the area rectangle.</param>
        /// <param name="width">The width of the area rectangle.</param>
        /// <param name="height">The height of the area rectangle.</param>
        public QuadTreeNode(int x, int y, int width, int height)
        {
            rect = new Rectangle(x, y, width, height);
        }


        private QuadTreeNode(QuadTreeNode<T> parent, Rectangle rect)
            : this(rect)
        {
            this.parent = parent;
        }

        /// <summary>
        /// Add an item to the object list.
        /// </summary>
        /// <param name="item">The item to add.</param>
        private void Add(QuadTreeObject<T> item)
        {
            if (objects == null)
            {
                objects = new List<QuadTreeObject<T>>();
            }

            item.Owner = this;
            objects.Add(item);
        }


        /// <summary>
        /// Remove an item from the object list.
        /// </summary>
        /// <param name="item">The object to remove.</param>
        private void Remove(QuadTreeObject<T> item)
        {
            if (objects != null)
            {
                var removeIndex = objects.IndexOf(item);
                if (removeIndex >= 0)
                {
                    objects[removeIndex] = objects[objects.Count - 1];
                    objects.RemoveAt(objects.Count - 1);
                }
            }
        }


        /// <summary>
        /// Get the total for all objects in this QuadTree, including children.
        /// </summary>
        /// <returns>The number of objects contained within this QuadTree and its children.</returns>
        private int ObjectCount()
        {
            var count = 0;

            if (objects != null)
            {
                count += objects.Count;
            }

            if (childTL != null)
            {
                count += childTL.ObjectCount();
                count += childTR.ObjectCount();
                count += childBL.ObjectCount();
                count += childBR.ObjectCount();
            }

            return count;
        }


        /// <summary>
        /// Subdivide this QuadTree and move it's children into the appropriate Quads where applicable.
        /// </summary>
        private void Subdivide()
        {
            var size = new Point(rect.Width / 2, rect.Height / 2);
            var mid = new Point(rect.X + size.X, rect.Y + size.Y);

            childTL = new QuadTreeNode<T>(this, new Rectangle(rect.Left, rect.Top, size.X, size.Y));
            childTR = new QuadTreeNode<T>(this, new Rectangle(mid.X, rect.Top, size.X, size.Y));
            childBL = new QuadTreeNode<T>(this, new Rectangle(rect.Left, mid.Y, size.X, size.Y));
            childBR = new QuadTreeNode<T>(this, new Rectangle(mid.X, mid.Y, size.X, size.Y));

            for (var i = 0; i < objects.Count; i++)
            {
                var destTree = GetDestinationTree(objects[i]);

                if (destTree != this)
                {
                    destTree.Insert(objects[i]);
                    Remove(objects[i]);
                    i--;
                }
            }
        }


        /// <summary>
        /// Get the child Quad that would contain an object.
        /// </summary>
        /// <param name="item">The object to get a child for.</param>
        /// <returns></returns>
        private QuadTreeNode<T> GetDestinationTree(QuadTreeObject<T> item)
        {
            var destTree = this;

            if (childTL.QuadRect.Contains(item.Data.Rect))
            {
                destTree = childTL;
            }
            else
            {
                if (childTR.QuadRect.Contains(item.Data.Rect))
                {
                    destTree = childTR;
                }
                else
                {
                    if (childBL.QuadRect.Contains(item.Data.Rect))
                    {
                        destTree = childBL;
                    }
                    else
                    {
                        if (childBR.QuadRect.Contains(item.Data.Rect))
                        {
                            destTree = childBR;
                        }
                    }
                }
            }
            return destTree;
        }


        private void Relocate(QuadTreeObject<T> item)
        {
            if (QuadRect.Contains(item.Data.Rect))
            {
                if (childTL != null)
                {
                    var dest = GetDestinationTree(item);
                    if (item.Owner != dest)
                    {
                        var formerOwner = item.Owner;
                        Delete(item, false);
                        dest.Insert(item);

                        formerOwner.CleanUpwards();
                    }
                }
            }
            else
            {
                if (parent != null)
                {
                    parent.Relocate(item);
                }
            }
        }


        private void CleanUpwards()
        {
            if (childTL != null)
            {
                if (childTL.IsEmptyLeaf &&
                    childTR.IsEmptyLeaf &&
                    childBL.IsEmptyLeaf &&
                    childBR.IsEmptyLeaf)
                {
                    childTL = null;
                    childTR = null;
                    childBL = null;
                    childBR = null;

                    if (parent != null && Count == 0)
                    {
                        parent.CleanUpwards();
                    }
                }
            }
            else
            {
                if (parent != null && Count == 0)
                {
                    parent.CleanUpwards();
                }
            }
        }

        /// <summary>
        /// Clears the QuadTree of all objects, including any objects living in its children.
        /// </summary>
        internal void Clear()
        {
            if (childTL != null)
            {
                childTL.Clear();
                childTR.Clear();
                childBL.Clear();
                childBR.Clear();
            }

            if (objects != null)
            {
                objects.Clear();
                objects = null;
            }

            childTL = null;
            childTR = null;
            childBL = null;
            childBR = null;
        }


        /// <summary>
        /// Deletes an item from this QuadTree. If the object is removed causes this Quad to have no objects in its children, it's children will be removed as well.
        /// </summary>
        /// <param name="item">The item to remove.</param>
        /// <param name="clean">Whether or not to clean the tree</param>
        internal void Delete(QuadTreeObject<T> item, bool clean)
        {
            if (item.Owner != null)
            {
                if (item.Owner == this)
                {
                    Remove(item);
                    if (clean)
                    {
                        CleanUpwards();
                    }
                }
                else
                {
                    item.Owner.Delete(item, clean);
                }
            }
        }



        /// <summary>
        /// Insert an item into this QuadTree object.
        /// </summary>
        /// <param name="item">The item to insert.</param>
        internal void Insert(QuadTreeObject<T> item)
        {
            if (!rect.Contains(item.Data.Rect))
            {
                System.Diagnostics.Debug.Assert(parent == null, "We are not the root, and this object doesn't fit here. How did we get here?");
                if (parent == null)
                {
                    Add(item);
                }
                else
                {
                    return;
                }
            }

            if (objects == null ||
                (childTL == null && objects.Count + 1 <= MaxObjectsPerNode))
            {
                Add(item);
            }
            else
            {
                if (childTL == null)
                {
                    Subdivide();
                }

                var destTree = GetDestinationTree(item);
                if (destTree == this)
                {
                    Add(item);
                }
                else
                {
                    destTree.Insert(item);
                }
            }
        }


        /// <summary>
        /// Get the objects in this tree that intersect with the specified rectangle.
        /// </summary>
        /// <param name="searchRect">The rectangle to find objects in.</param>
        internal List<T> GetObjects(Rectangle searchRect)
        {
            var results = new List<T>();
            GetObjects(searchRect, ref results);
            return results;
        }


        /// <summary>
        /// Get the objects in this tree that intersect with the specified rectangle.
        /// </summary>
        /// <param name="searchRect">The rectangle to find objects in.</param>
        /// <param name="results">A reference to a list that will be populated with the results.</param>
        internal void GetObjects(Rectangle searchRect, ref List<T> results)
        {
            if (results != null)
            {
                if (searchRect.Contains(rect))
                {
                    GetAllObjects(ref results);
                }
                else
                {
                    if (searchRect.IntersectsWith(rect))
                    {
                        if (objects != null)
                        {
                            for (var i = 0; i < objects.Count; i++)
                            {
                                if (searchRect.IntersectsWith(objects[i].Data.Rect))
                                {
                                    results.Add(objects[i].Data);
                                }
                            }
                        }

                        if (childTL != null)
                        {
                            childTL.GetObjects(searchRect, ref results);
                            childTR.GetObjects(searchRect, ref results);
                            childBL.GetObjects(searchRect, ref results);
                            childBR.GetObjects(searchRect, ref results);
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Get all objects in this Quad, and it's children.
        /// </summary>
        /// <param name="results">A reference to a list in which to store the objects.</param>
        internal void GetAllObjects(ref List<T> results)
        {
            if (objects != null)
            {
                foreach (QuadTreeObject<T> qto in objects)
                {
                    results.Add(qto.Data);
                }
            }

            if (childTL != null)
            {
                childTL.GetAllObjects(ref results);
                childTR.GetAllObjects(ref results);
                childBL.GetAllObjects(ref results);
                childBR.GetAllObjects(ref results);
            }
        }


        /// <summary>
        /// Moves the QuadTree object in the tree
        /// </summary>
        /// <param name="item">The item that has moved</param>
        internal void Move(QuadTreeObject<T> item)
        {
            if (item.Owner != null)
            {
                item.Owner.Relocate(item);
            }
            else
            {
                Relocate(item);
            }
        }
    }
}
