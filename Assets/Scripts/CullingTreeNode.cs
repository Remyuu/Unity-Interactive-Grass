using System.Collections.Generic;
using UnityEngine;

public class CullingTreeNode
{
    public Bounds m_bounds;

    public CullingTreeNode m_parent;

    public List<CullingTreeNode> m_children = new List<CullingTreeNode>();

    public List<int> grassIDHeld = new List<int>();

    public CullingTreeNode(Bounds bounds, int depth)
    {
        m_children.Clear();
        m_bounds = bounds;

        if (depth > 0)
        {
            Vector3 size = m_bounds.size;
            size /= 4.0f;
            Vector3 childSize = m_bounds.size / 2.0f;
            Vector3 center = m_bounds.center;

            // needs less subdiv in the y axis, if you have a LOT of verticality, delete this if statement)
            if (depth % 2 == 0)
            {
                childSize.y = m_bounds.size.y;
                Bounds topLeftSingle = new Bounds(new Vector3(center.x - size.x, center.y, center.z - size.z), childSize);
                Bounds bottomRightSingle = new Bounds(new Vector3(center.x + size.x, center.y, center.z + size.z), childSize);
                Bounds topRightSingle = new Bounds(new Vector3(center.x - size.x, center.y, center.z + size.z), childSize);
                Bounds bottomLeftSingle = new Bounds(new Vector3(center.x + size.x, center.y, center.z - size.z), childSize);

                m_children.Add(new CullingTreeNode(topLeftSingle, depth - 1));
                m_children.Add(new CullingTreeNode(bottomRightSingle, depth - 1));
                m_children.Add(new CullingTreeNode(topRightSingle, depth - 1));
                m_children.Add(new CullingTreeNode(bottomLeftSingle, depth - 1));
            }
            else
            {
                // // layer 1
                Bounds topLeft = new Bounds(new Vector3(center.x - size.x, center.y - size.y, center.z - size.z), childSize);
                Bounds bottomRight = new Bounds(new Vector3(center.x + size.x, center.y - size.y, center.z + size.z), childSize);
                Bounds topRight = new Bounds(new Vector3(center.x - size.x, center.y - size.y, center.z + size.z), childSize);
                Bounds bottomLeft = new Bounds(new Vector3(center.x + size.x, center.y - size.y, center.z - size.z), childSize);

                // // layer 2
                Bounds topLeft2 = new Bounds(new Vector3(center.x - size.x, center.y + size.y, center.z - size.z), childSize);
                Bounds bottomRight2 = new Bounds(new Vector3(center.x + size.x, center.y + size.y, center.z + size.z), childSize);
                Bounds topRight2 = new Bounds(new Vector3(center.x - size.x, center.y + size.y, center.z + size.z), childSize);
                Bounds bottomLeft2 = new Bounds(new Vector3(center.x + size.x, center.y + size.y, center.z - size.z), childSize);

                m_children.Add(new CullingTreeNode(topLeft, depth - 1));
                m_children.Add(new CullingTreeNode(bottomRight, depth - 1));
                m_children.Add(new CullingTreeNode(topRight, depth - 1));
                m_children.Add(new CullingTreeNode(bottomLeft, depth - 1));

                m_children.Add(new CullingTreeNode(topLeft2, depth - 1));
                m_children.Add(new CullingTreeNode(bottomRight2, depth - 1));
                m_children.Add(new CullingTreeNode(topRight2, depth - 1));
                m_children.Add(new CullingTreeNode(bottomLeft2, depth - 1));
            }



        }
    }


    public void RetrieveLeaves(Plane[] frustum, List<Bounds> list, List<int> visibleIDList)
    {
        if (GeometryUtility.TestPlanesAABB(frustum, m_bounds))
        {
            if (m_children.Count == 0)
            {
                if (grassIDHeld.Count > 0)
                {
                    list.Add(m_bounds);
                    visibleIDList.AddRange(grassIDHeld);
                }
            }
            else
            {
                foreach (CullingTreeNode child in m_children)
                {
                    child.RetrieveLeaves(frustum, list, visibleIDList);
                }
            }
        }
    }


    public bool FindLeaf(Vector3 point, int index)
    {
        bool FoundSpot = false;
        if (m_bounds.Contains(point))
        {
            if (m_children.Count != 0)
            {
                foreach (CullingTreeNode child in m_children)
                {
                    if (child.FindLeaf(point, index))
                    {
                        return true;
                    }
                }
            }
            else
            {
                grassIDHeld.Add(index);
                return true;
            }
        }
        return FoundSpot;
    }

    public void RetrieveAllLeaves(List<CullingTreeNode> target)
    {
        if (m_children.Count == 0)
        {
            target.Add(this);
        }
        else
        {
            foreach (CullingTreeNode child in m_children)
            {
                child.RetrieveAllLeaves(target);
            }
        }
    }

    public bool ClearEmpty()
    {
        bool delete = false;
        if (m_children.Count > 0)
        {
            //  DownSize();
            int i = m_children.Count - 1;
            while (i > 0)
            {
                if (m_children[i].ClearEmpty())
                {
                    m_children.RemoveAt(i);
                }
                i--;
            }
        }
        if (grassIDHeld.Count == 0 && m_children.Count == 0)
        {
            delete = true;
        }
        return delete;
    }

    // added for cutting
    public void ReturnLeafList(Vector3 point, List<int> grassList, float radius)
    {
        Bounds expandedBounds = m_bounds;
        expandedBounds.Expand(radius * 2);
        if (!expandedBounds.Contains(point))
        {
            return; // hit point is outside the bounds
        }

        if (m_children.Count == 0)
        {
            grassList.AddRange(grassIDHeld);
        }
        else
        {
            foreach (CullingTreeNode child in m_children)
            {
                Bounds expandedBoundsChild = child.m_bounds;
                expandedBoundsChild.Expand(radius * 2);
                if (expandedBoundsChild.Contains(point))
                {
                    child.ReturnLeafList(point, grassList, radius);
                }
            }
        }
    }
}