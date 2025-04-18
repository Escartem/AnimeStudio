﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AnimeStudio
{
    public class FileIdentifier
    {
        public Guid guid;
        public int type; //enum { kNonAssetType = 0, kDeprecatedCachedAssetType = 1, kSerializedAssetType = 2, kMetaAssetType = 3 };
        public string pathName;

        //custom
        public string fileName;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"Guid: {guid} | ");
            sb.Append($"type: {type} | ");
            sb.Append($"pathName: {pathName} | ");
            sb.Append($"fileName: {fileName}");
            return sb.ToString();
        }
    }
}
