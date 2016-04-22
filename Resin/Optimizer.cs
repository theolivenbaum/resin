//using System;
//using System.Collections.Generic;
//using System.IO;
//using Resin.IO;

//namespace Resin
//{
//    /// <summary>
//    /// Initialize an Optimizer with your directory's current baseline and then fast-forward to the directory's latest commit by calling Optimizer.Rebase().
//    /// Save that state as a new baseline by calling Optimizer.Save().
//    /// </summary>
//    public class Optimizer
//    {
//        //private static readonly ILog Log = LogManager.GetLogger(typeof(Optimizer));
//        private readonly string _directory;
//        private readonly Dictionary<string, FieldFile> _fieldFilesByFileId;
//        private readonly Dictionary<string, Trie> _triesByFileId;
//        private readonly Dictionary<string, List<string>> _fieldToFileIds;
//        private readonly Dictionary<string, List<string>>_docIdToFileIds;

//        public Optimizer(string directory, Dictionary<string, FieldFile> fieldFilesByFileId, Dictionary<string, Trie> triesByFileId, Dictionary<string, List<string>> fieldToFileIds, Dictionary<string, List<string>> docIdToFileIds)
//        {
//            _fieldFilesByFileId = fieldFilesByFileId;
//            _triesByFileId = triesByFileId;
//            _fieldToFileIds = fieldToFileIds;
//            _docIdToFileIds = docIdToFileIds;
//            _directory = directory;
//        }

//        /// <summary>
//        /// Rewind the state of your directory to a commit older than the current baseline.
//        /// </summary>
//        /// <param name="indexOrCommitFileName"></param>
//        public void RebaseHard(string indexOrCommitFileName)
//        {
//            //TODO: implement set head
//        }

//        /// <summary>
//        /// Rewind the state of your directory to a commit older than the current baseline.
//        /// </summary>
//        /// <param name="commitFileName"></param>
//        public void RewindTo(string commitFileName)
//        {
//            //TODO: implement rewind
//        }

//        /// <summary>
//        /// Irreversibly delete commits that are older than the latest baseline.
//        /// </summary>
//        public void Truncate()
//        {
//            //TODO: implement truncate
//        }

//        public void FastForward()
//        {
//            var indices = Helper.GetFilesOrderedChronologically(_directory, "*.ix");
//            foreach (var ixFileName in indices)
//            {
//                var ix = IxFile.Load(Path.Combine(_directory, ixFileName));
//                var fix = FixFile.Load(Path.Combine(_directory, ix.FixFileName));

//                // load all field and trie files
//                // execute delete
//                foreach (var field in fix.FieldToFileId)
//                {
//                    List<string> fileIds;
//                    if (_fieldToFileIds.TryGetValue(field.Key, out fileIds))
//                    {
//                        fileIds.Add(field.Value);
//                    }
//                    else
//                    {
//                        _fieldToFileIds.Add(field.Key, new List<string> { field.Value });
//                    }
//                    var fieldFile = FieldFile.Load(Path.Combine(_directory, field.Value + ".f"));
//                    foreach (var docId in ix.DeletedDocs)
//                    {
//                        fieldFile.Remove(docId);
//                    }
//                    _fieldFilesByFileId.Add(field.Value, fieldFile);

//                    var trieFile = Trie.Load(Path.Combine(_directory, field.Value + ".f.tri"));
//                    _triesByFileId.Add(field.Value, trieFile);
//                }

//                // a document can exist in many versions
//                // find and add all versions to the doc index
//                var dix = DixFile.Load(Path.Combine(_directory, ix.DixFileName));
//                foreach (var d in dix.DocIdToFileId)
//                {
//                    List<string> fileIds;
//                    if (_docIdToFileIds.TryGetValue(d.Key, out fileIds))
//                    {
//                        fileIds.Add(d.Value);
//                    }
//                    else
//                    {
//                        _docIdToFileIds[d.Key] = new List<string> { d.Value };
//                    }
//                }
//            }
//        }

//        public IxFile Save()
//        {
//            throw new NotImplementedException();
//        }
//    }
//}