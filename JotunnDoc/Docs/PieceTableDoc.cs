﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using JotunnLib.Managers;
using JotunnLib.Utils;

namespace JotunnDoc.Docs
{
    public class PieceTableDoc : Doc
    {
        public PieceTableDoc() : base("JotunnDoc/Docs/conceptual/pieces/piece-table-list.md")
        {
            PieceManager.Instance.PieceTableRegister += docPieceTables;
        }

        public void docPieceTables(object sender, EventArgs e)
        {
            Debug.Log("Documenting piece tables");

            AddHeader(1, "Piece table list");
            AddText("All of the piece tables currently in the game.");
            AddText("This file is automatically generated from Valheim using the JotunnDoc mod found on our GitHub.");
            AddTableHeader("GameObject Name", "JotunnLib Alias", "Piece Count");

            var pieceTables = ReflectionUtils.GetPrivateField<Dictionary<string, PieceTable>>(PieceManager.Instance, "pieceTables");
            var nameMap = ReflectionUtils.GetPrivateField<Dictionary<string, string>>(PieceManager.Instance, "pieceTableNameMap");

            foreach (var pair in pieceTables)
            {
                string alias = "";

                if (nameMap.ContainsValue(pair.Key))
                {
                    foreach (string key in nameMap.Keys)
                    {
                        if (nameMap[key] == pair.Key)
                        {
                            alias = key;
                            break;
                        }
                    }
                }

                AddTableRow(pair.Key, alias, pair.Value.m_pieces.Count.ToString());
            }

            Save();
        }
    }
}
