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

/* NOTES:
 * ------
 * This quad tree was developed as a generically typed quad tree for use with
 * the Microsoft XNA framework. To that end, it references
 * Microsoft.Xna.Framework.Rectangle to supply the functionality for defining a
 * rectangle as well as providing the Contains and Intersects methods used for
 * determining what is in a quad or not.
 *
 * This code can quite easily be modified to remove the dependence on the XNA
 * framework by removing the reference and updating anywhere that the rectangle
 * is used. The rest should function as is.
 */

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace Hybrasyl;

/// <summary>
///     Interface to define Rect, so that QuadTree knows how to store the object.
/// </summary>
public interface IQuadStorable
{
    /// <summary>
    ///     The rectangle that defines the object's boundaries.
    /// </summary>
    Rectangle Rect { get; }

    /// <summary>
    ///     This should return True if the object has moved during the last update, false otherwise
    /// </summary>
    bool HasMoved { get; }
}

/// <summary>
///     Used internally to attach an Owner to each object stored in the QuadTree
/// </summary>
/// <typeparam name="T"></typeparam>
internal class QuadTreeObject<T> where T : IQuadStorable //, IComparable<QuadTreeObject<T>>
{
    /// <summary>
    ///     Wraps the data value
    /// </summary>
    /// <param name="data">The data value to wrap</param>
    public QuadTreeObject(T data)
    {
        Data = data;
    }

    /// <summary>
    ///     The wrapped data value
    /// </summary>
    public T Data { get; }

    /// <summary>
    ///     The QuadTreeNode that owns this object
    /// </summary>
    internal QuadTreeNode<T> Owner { get; set; }


    //public int CompareTo(QuadTreeObject<T> other)
    //{
    //    return (int)(Data.Rect.Y + Data.Rect.Height) - (int)(other.Data.Rect.Y + other.Data.Rect.Height);
    //}
}

/// <summary>
///     A QuadTree Object that provides fast and efficient storage of objects in a world space.
/// </summary>
/// <typeparam name="T">Any object implementing IQuadStorable.</typeparam>
public class QuadTree<T> : ICollection<T> where T : IQuadStorable
{
    #region Private Members

    private readonly Dictionary<T, QuadTreeObject<T>> wrappedDictionary = new();

    // Alternate method, use Parallel arrays
    //private List<T> m_rawObjects = new List<T>();       // The unwrapped objects in this QuadTree
    //private List<QuadTreeObject<T>> m_wrappedObjects = new List<QuadTreeObject<T>>();       // The wrapped objects in this QuadTree

    // The root of this quad tree

    #endregion

    /// <summary>
    ///     The top left child for this QuadTree, only usable in debug mode
    /// </summary>
    public QuadTreeNode<T> RootQuad { get; }

    #region Constructor

    /// <summary>
    ///     Creates a QuadTree for the specified area.
    /// </summary>
    /// <param name="rect">The area this QuadTree object will encompass.</param>
    public QuadTree(Rectangle rect)
    {
        RootQuad = new QuadTreeNode<T>(rect);
    }


    /// <summary>
    ///     Creates a QuadTree for the specified area.
    /// </summary>
    /// <param name="x">The top-left position of the area rectangle.</param>
    /// <param name="y">The top-right position of the area rectangle.</param>
    /// <param name="width">The width of the area rectangle.</param>
    /// <param name="height">The height of the area rectangle.</param>
    public QuadTree(int x, int y, int width, int height)
    {
        RootQuad = new QuadTreeNode<T>(new Rectangle(x, y, width, height));
    }

    #endregion

    #region Public Methods

    /// <summary>
    ///     Gets the rectangle that bounds this QuadTree
    /// </summary>
    public Rectangle QuadRect => RootQuad.QuadRect;

    /// <summary>
    ///     Get the objects in this tree that intersect with the specified rectangle.
    /// </summary>
    /// <param name="rect">The rectangle to find objects in.</param>
    public List<T> GetObjects(Rectangle rect) => RootQuad.GetObjects(rect);


    ///// <summary>
    ///// Get the objects in this tree that intersect with the specified rectangle.
    ///// </summary>
    ///// <param name="rect">The rectangle to find objects in.</param>
    ///// <param name="results">A reference to a list that will be populated with the results.</param>
    //public void GetObjects(Rectangle rect, ref List<T> results)
    //{
    //    quadTreeRoot.GetObjects(rect, ref results);
    //}


    /// <summary>
    ///     Get all objects in this Quad, and it's children.
    /// </summary>
    public List<T> GetAllObjects() => new(wrappedDictionary.Keys);
    //quadTreeRoot.GetAllObjects(ref results);

    /// <summary>
    ///     Moves the object in the tree
    /// </summary>
    /// <param name="item">The item that has moved</param>
    public bool Move(T item)
    {
        if (Contains(item))
        {
            RootQuad.Move(wrappedDictionary[item]);
            return true;
        }

        return false;
    }

    #endregion

    #region ICollection<T> Members

    /// <summary>
    ///     Adds an item to the <see cref="T:System.Collections.Generic.ICollection`1" />.
    /// </summary>
    /// <param name="item">The object to add to the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
    /// <exception cref="T:System.NotSupportedException">
    ///     The <see cref="T:System.Collections.Generic.ICollection`1" /> is
    ///     read-only.
    /// </exception>
    public void Add(T item)
    {
        var wrappedObject = new QuadTreeObject<T>(item);
        wrappedDictionary.Add(item, wrappedObject);
        RootQuad.Insert(wrappedObject);
    }


    /// <summary>
    ///     Removes all items from the <see cref="T:System.Collections.Generic.ICollection`1" />.
    /// </summary>
    /// <exception cref="T:System.NotSupportedException">
    ///     The <see cref="T:System.Collections.Generic.ICollection`1" /> is
    ///     read-only.
    /// </exception>
    public void Clear()
    {
        wrappedDictionary.Clear();
        RootQuad.Clear();
    }


    /// <summary>
    ///     Determines whether the <see cref="T:System.Collections.Generic.ICollection`1" /> contains a specific value.
    /// </summary>
    /// <returns>
    ///     true if <paramref name="item" /> is found in the <see cref="T:System.Collections.Generic.ICollection`1" />;
    ///     otherwise, false.
    /// </returns>
    /// <param name="item">The object to locate in the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
    public bool Contains(T item) => wrappedDictionary.ContainsKey(item);


    /// <summary>
    ///     Copies the elements of the <see cref="T:System.Collections.Generic.ICollection`1" /> to an
    ///     <see cref="T:System.Array" />, starting at a particular <see cref="T:System.Array" /> index.
    /// </summary>
    /// <param name="array">
    ///     The one-dimensional <see cref="T:System.Array" /> that is the destination of the elements copied
    ///     from <see cref="T:System.Collections.Generic.ICollection`1" />. The <see cref="T:System.Array" /> must have
    ///     zero-based indexing.
    /// </param>
    /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="array" /> is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="arrayIndex" /> is less than 0.</exception>
    /// <exception cref="T:System.ArgumentException">
    ///     <paramref name="array" /> is multidimensional.-or-
    ///     <paramref name="arrayIndex" /> is equal to or greater than the length of <paramref name="array" />.-or-The number
    ///     of elements in the source <see cref="T:System.Collections.Generic.ICollection`1" /> is greater than the available
    ///     space from <paramref name="arrayIndex" /> to the end of the destination <paramref name="array" />.-or-Type
    ///     <paramref name="T" /> cannot be cast automatically to the type of the destination <paramref name="array" />.
    /// </exception>
    public void CopyTo(T[] array, int arrayIndex)
    {
        wrappedDictionary.Keys.CopyTo(array, arrayIndex);
    }

    /// <summary>
    ///     Gets the number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1" />.
    /// </summary>
    /// <returns>
    ///     The number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1" />.
    /// </returns>
    public int Count => wrappedDictionary.Count;

    /// <summary>
    ///     Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.
    /// </summary>
    /// <returns>
    ///     true if the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only; otherwise, false.
    /// </returns>
    public bool IsReadOnly => false;

    /// <summary>
    ///     Removes the first occurrence of a specific object from the
    ///     <see cref="T:System.Collections.Generic.ICollection`1" />.
    /// </summary>
    /// <returns>
    ///     true if <paramref name="item" /> was successfully removed from the
    ///     <see cref="T:System.Collections.Generic.ICollection`1" />; otherwise, false. This method also returns false if
    ///     <paramref name="item" /> is not found in the original <see cref="T:System.Collections.Generic.ICollection`1" />.
    /// </returns>
    /// <param name="item">The object to remove from the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
    /// <exception cref="T:System.NotSupportedException">
    ///     The <see cref="T:System.Collections.Generic.ICollection`1" /> is
    ///     read-only.
    /// </exception>
    public bool Remove(T item)
    {
        if (Contains(item))
        {
            RootQuad.Delete(wrappedDictionary[item], true);
            wrappedDictionary.Remove(item);
            return true;
        }

        return false;
    }

    #endregion

    #region IEnumerable<T> and IEnumerable Members

    /// <summary>
    ///     Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>
    ///     A <see cref="T:System.Collections.Generic.IEnumerator`1" /> that can be used to iterate through the collection.
    /// </returns>
    /// <filterpriority>1</filterpriority>
    public IEnumerator<T> GetEnumerator() => wrappedDictionary.Keys.GetEnumerator();


    /// <summary>
    ///     Returns an enumerator that iterates through a collection.
    /// </summary>
    /// <returns>
    ///     An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.
    /// </returns>
    /// <filterpriority>2</filterpriority>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion
}

/// <summary>
///     A QuadTree Object that provides fast and efficient storage of objects in a world space.
/// </summary>
/// <typeparam name="T">Any object implementing IQuadStorable.</typeparam>
public class QuadTreeNode<T> where T : IQuadStorable
{
    #region Constants

    // How many objects can exist in a QuadTree before it sub divides itself
    private const int MaxObjectsPerNode = 2;

    #endregion

    #region Private Members

    //private List<T> m_objects = null;       // The objects in this QuadTree
    private readonly Rectangle rect; // The area this QuadTree represents

    private readonly object lockObject = new();

    #endregion

    #region Public Properties

    /// <summary>
    ///     The area this QuadTree represents.
    /// </summary>
    public Rectangle QuadRect => rect;

    /// <summary>
    ///     The top left child for this QuadTree
    /// </summary>
    public QuadTreeNode<T> TopLeftChild { get; private set; }

    /// <summary>
    ///     The top right child for this QuadTree
    /// </summary>
    public QuadTreeNode<T> TopRightChild { get; private set; }

    /// <summary>
    ///     The bottom left child for this QuadTree
    /// </summary>
    public QuadTreeNode<T> BottomLeftChild { get; private set; }

    /// <summary>
    ///     The bottom right child for this QuadTree
    /// </summary>
    public QuadTreeNode<T> BottomRightChild { get; private set; }

    /// <summary>
    ///     This QuadTree's parent
    /// </summary>
    public QuadTreeNode<T> Parent { get; }

    /// <summary>
    ///     The objects contained in this QuadTree at it's level (ie, excludes children)
    /// </summary>
    //public List<T> Objects { get { return m_objects; } }
    internal List<QuadTreeObject<T>> Objects { get; private set; }

    /// <summary>
    ///     How many total objects are contained within this QuadTree (ie, includes children)
    /// </summary>
    public int Count => ObjectCount();

    /// <summary>
    ///     Returns true if this is a empty leaf node
    /// </summary>
    public bool IsEmptyLeaf => Count == 0 && TopLeftChild == null;

    #endregion

    #region Constructor

    /// <summary>
    ///     Creates a QuadTree for the specified area.
    /// </summary>
    /// <param name="rect">The area this QuadTree object will encompass.</param>
    public QuadTreeNode(Rectangle rect)
    {
        this.rect = rect;
    }


    /// <summary>
    ///     Creates a QuadTree for the specified area.
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
        Parent = parent;
    }

    #endregion

    #region Private Members

    /// <summary>
    ///     Add an item to the object list.
    /// </summary>
    /// <param name="item">The item to add.</param>
    private void Add(QuadTreeObject<T> item)
    {
        if (Objects == null)
            //m_objects = new List<T>();
            Objects = new List<QuadTreeObject<T>>();

        item.Owner = this;
        lock (lockObject)
        {
            Objects.Add(item);
        }
    }

    /// <summary>
    ///     Remove an item from the object list.
    /// </summary>
    /// <param name="item">The object to remove.</param>
    private void Remove(QuadTreeObject<T> item)
    {
        if (Objects != null)
        {
            var removeIndex = Objects.IndexOf(item);
            if (removeIndex >= 0)
                // We have to consider the case that a tile contains: User, Item1, Item2.  The user can Walk, which removes him from index 0.
                // We should use objects.Remove( index ) and let it "shift up" the remaining objects for us.

                //objects[removeIndex] = objects[objects.Count - 1]; 
                //objects.RemoveAt(objects.Count - 1);

                // We know the item, so just remove it by its index.
                lock (lockObject)
                {
                    Objects.RemoveAt(removeIndex);
                }
        }
    }


    /// <summary>
    ///     Get the total for all objects in this QuadTree, including children.
    /// </summary>
    /// <returns>The number of objects contained within this QuadTree and its children.</returns>
    private int ObjectCount()
    {
        var count = 0;

        // Add the objects at this level
        if (Objects != null) count += Objects.Count;

        // Add the objects that are contained in the children
        if (TopLeftChild != null)
        {
            count += TopLeftChild.ObjectCount();
            count += TopRightChild.ObjectCount();
            count += BottomLeftChild.ObjectCount();
            count += BottomRightChild.ObjectCount();
        }

        return count;
    }


    /// <summary>
    ///     Subdivide this QuadTree and move it's children into the appropriate Quads where applicable.
    /// </summary>
    private void Subdivide()
    {
        // We've reached capacity, subdivide...


        var size = new System.Drawing.Point(rect.Width / 2, rect.Height / 2);
        var mid = new System.Drawing.Point(rect.X + size.X, rect.Y + size.Y);

        TopLeftChild = new QuadTreeNode<T>(this, new Rectangle(rect.Left, rect.Top, size.X, size.Y));
        TopRightChild = new QuadTreeNode<T>(this, new Rectangle(mid.X, rect.Top, size.X, size.Y));
        BottomLeftChild = new QuadTreeNode<T>(this, new Rectangle(rect.Left, mid.Y, size.X, size.Y));
        BottomRightChild = new QuadTreeNode<T>(this, new Rectangle(mid.X, mid.Y, size.X, size.Y));

        // If they're completely contained by the quad, bump objects down
        lock (lockObject)
        {
            for (var i = 0; i < Objects.Count; i++)
            {
                var destTree = GetDestinationTree(Objects[i]);

                if (destTree != this)
                {
                    // Insert to the appropriate tree, remove the object, and back up one in the loop
                    destTree.Insert(Objects[i]);
                    Remove(Objects[i]);
                    i--;
                }
            }
        }
    }


    /// <summary>
    ///     Get the child Quad that would contain an object.
    /// </summary>
    /// <param name="item">The object to get a child for.</param>
    /// <returns></returns>
    private QuadTreeNode<T> GetDestinationTree(QuadTreeObject<T> item)
    {
        // If a child can't contain an object, it will live in this Quad
        var destTree = this;

        if (TopLeftChild.QuadRect.Contains(item.Data.Rect))
            destTree = TopLeftChild;
        else if (TopRightChild.QuadRect.Contains(item.Data.Rect))
            destTree = TopRightChild;
        else if (BottomLeftChild.QuadRect.Contains(item.Data.Rect))
            destTree = BottomLeftChild;
        else if (BottomRightChild.QuadRect.Contains(item.Data.Rect)) destTree = BottomRightChild;

        return destTree;
    }


    private void Relocate(QuadTreeObject<T> item)
    {
        // Are we still inside our parent?
        if (QuadRect.Contains(item.Data.Rect))
        {
            // Good, have we moved inside any of our children?
            if (TopLeftChild != null)
            {
                var dest = GetDestinationTree(item);
                if (item.Owner != dest)
                {
                    // Delete the item from this quad and add it to our child
                    // Note: Do NOT clean during this call, it can potentially delete our destination quad
                    var formerOwner = item.Owner;

                    Delete(item, false);
                    dest.Insert(item);

                    // Clean up ourselves
                    formerOwner.CleanUpwards();
                }
            }
        }
        else
        {
            // We don't fit here anymore, move up, if we can
            if (Parent != null) Parent.Relocate(item);
        }
    }


    private void CleanUpwards()
    {
        if (TopLeftChild != null)
        {
            // If all the children are empty leaves, delete all the children
            if (TopLeftChild.IsEmptyLeaf &&
                TopRightChild.IsEmptyLeaf &&
                BottomLeftChild.IsEmptyLeaf &&
                BottomRightChild.IsEmptyLeaf)
            {
                lock (lockObject)
                {
                    TopLeftChild = null;
                    TopRightChild = null;
                    BottomLeftChild = null;
                    BottomRightChild = null;
                }

                if (Parent != null && Count == 0) Parent.CleanUpwards();
            }
        }
        else
        {
            // I could be one of 4 empty leaves, tell my parent to clean up
            if (Parent != null && Count == 0) Parent.CleanUpwards();
        }
    }

    #endregion

    #region Internal Methods

    /// <summary>
    ///     Clears the QuadTree of all objects, including any objects living in its children.
    /// </summary>
    internal void Clear()
    {
        // Clear out the children, if we have any
        if (TopLeftChild != null)
            lock (lockObject)
            {
                TopLeftChild.Clear();
                TopRightChild.Clear();
                BottomLeftChild.Clear();
                BottomRightChild.Clear();
            }

        // Clear any objects at this level
        if (Objects != null)
            lock (lockObject)
            {
                Objects.Clear();
                Objects = null;
            }

        // Set the children to null
        lock (lockObject)
        {
            TopLeftChild = null;
            TopRightChild = null;
            BottomLeftChild = null;
            BottomRightChild = null;
        }
    }


    /// <summary>
    ///     Deletes an item from this QuadTree. If the object is removed causes this Quad to have no objects in its children,
    ///     it's children will be removed as well.
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
                if (clean) CleanUpwards();
            }
            else
            {
                item.Owner.Delete(item, clean);
            }
        }
    }


    /// <summary>
    ///     Insert an item into this QuadTree object.
    /// </summary>
    /// <param name="item">The item to insert.</param>
    internal void Insert(QuadTreeObject<T> item)
    {
        // If this quad doesn't contain the items rectangle, do nothing, unless we are the root
        if (!rect.Contains(item.Data.Rect))
        {
            Debug.Assert(Parent == null, "We are not the root, and this object doesn't fit here. How did we get here?");
            if (Parent == null)
                // This object is outside of the QuadTree bounds, we should add it at the root level
                Add(item);
            else
                return;
        }

        if (Objects == null ||
            (TopLeftChild == null && Objects.Count + 1 <= MaxObjectsPerNode))
        {
            // If there's room to add the object, just add it
            Add(item);
        }
        else
        {
            // No quads, create them and bump objects down where appropriate
            if (TopLeftChild == null) Subdivide();

            // Find out which tree this object should go in and add it there
            var destTree = GetDestinationTree(item);
            if (destTree == this)
                Add(item);
            else
                destTree.Insert(item);
        }
    }


    /// <summary>
    ///     Get the objects in this tree that intersect with the specified rectangle.
    /// </summary>
    /// <param name="searchRect">The rectangle to find objects in.</param>
    internal List<T> GetObjects(Rectangle searchRect)
    {
        var results = new List<T>();
        lock (lockObject)
        {
            GetObjects(searchRect, ref results);
        }

        return results;
    }


    /// <summary>
    ///     Get the objects in this tree that intersect with the specified rectangle.
    /// </summary>
    /// <param name="searchRect">The rectangle to find objects in.</param>
    /// <param name="results">A reference to a list that will be populated with the results.</param>
    internal void GetObjects(Rectangle searchRect, ref List<T> results)
    {
        // We can't do anything if the results list doesn't exist
        if (results == null) return;
        if (searchRect.IsEmpty) return;
        if (searchRect.Contains(rect))
        {
            // If the search area completely contains this quad, just get every object this quad and all it's children have
            GetAllObjects(ref results);
        }
        else if (searchRect.IntersectsWith(rect))
        {
            // Otherwise, if the quad isn't fully contained, only add objects that intersect with the search rectangle
            if (Objects != null)
                lock (lockObject)
                {
                    for (var i = 0; i < Objects.Count; i++)
                        if (searchRect.IntersectsWith(Objects[i].Data.Rect))
                            results.Add(Objects[i].Data);
                }

            // Get the objects for the search rectangle from the children
            if (TopLeftChild != null)
            {
                TopLeftChild?.GetObjects(searchRect, ref results);
                TopRightChild?.GetObjects(searchRect, ref results);
                BottomLeftChild?.GetObjects(searchRect, ref results);
                BottomRightChild?.GetObjects(searchRect, ref results);
            }
        }
    }


    /// <summary>
    ///     Get all objects in this Quad, and it's children.
    /// </summary>
    /// <param name="results">A reference to a list in which to store the objects.</param>
    internal void GetAllObjects(ref List<T> results)
    {
        // If this Quad has objects, add them
        if (Objects != null)
            lock (lockObject)
            {
                foreach (var qto in Objects) results.Add(qto.Data);
            }

        // If we have children, get their objects too
        if (TopLeftChild != null)
        {
            TopLeftChild.GetAllObjects(ref results);
            TopRightChild.GetAllObjects(ref results);
            BottomLeftChild.GetAllObjects(ref results);
            BottomRightChild.GetAllObjects(ref results);
        }
    }


    /// <summary>
    ///     Moves the QuadTree object in the tree
    /// </summary>
    /// <param name="item">The item that has moved</param>
    internal void Move(QuadTreeObject<T> item)
    {
        if (item.Owner != null)
            item.Owner.Relocate(item);
        else
            Relocate(item);
    }

    #endregion
}