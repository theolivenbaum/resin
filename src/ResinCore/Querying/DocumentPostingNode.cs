using System.Collections.Generic;
using System.Diagnostics;

namespace Resin.Querying
{
    [DebuggerDisplay("{Data}")]
    public class DocumentPostingNode
    {
        public DocumentPosting Data;
        public DocumentPostingNode Left, Right;

        public DocumentPostingNode():this(new DocumentPosting(-1, -1))
        {
        }

        public DocumentPostingNode(DocumentPosting data)
        {
            Data = data;
        }

        public void Add(DocumentPosting data)
        {
            var node = this;

            while (true)
            {
                if (data.DocumentId < node.Data.DocumentId) 
                {
                    if (node.Left == null)
                    {
                        node.Left = new DocumentPostingNode(data);
                        break;
                    }
                    else
                    {
                        node = node.Left;
                    }
                }
                else if (data.DocumentId > node.Data.DocumentId)
                {
                    if (node.Right == null)
                    {
                        node.Right = new DocumentPostingNode(data);
                        break;
                    }
                    else
                    {
                        node = node.Right;
                    }
                }
                else
                {
                    if (data.Data < node.Data.Data)
                    {
                        if (node.Left == null)
                        {
                            node.Left = new DocumentPostingNode(data);
                            break;
                        }
                        else
                        {
                            node = node.Left;
                        }
                    }
                    else
                    {
                        if (node.Right == null)
                        {
                            node.Right = new DocumentPostingNode(data);
                            break;
                        }
                        else
                        {
                            node = node.Right;
                        }
                    }
                }
            }
        }

        public IList<DocumentPosting> Sorted()
        {
            var stack = new Stack<DocumentPostingNode>();
            var queue = new Queue<DocumentPostingNode>();
            var node = Left;

            if (Left == null)
            {
                if (Right != null)
                {
                    node = Right;
                }
            }

            while (node != null)
            {
                queue.Enqueue(node);

                if (node.Right != null)
                {
                    stack.Push(node.Right);
                }

                if (node.Left != null)
                {
                    queue.Enqueue(node.Left);
                    node = node.Left;
                }
                else if (stack.Count > 0)
                {
                    node = stack.Pop();
                }
                else
                {
                    break;
                }
            }

            var result = new List<DocumentPosting>();

            foreach (var n in queue)
            {
                result.Add(n.Data);
            }
            return result;
        }
    }
}
