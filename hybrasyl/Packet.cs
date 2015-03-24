﻿/*
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
 * (C) 2013 Justin Baugh (baughj@hybrasyl.com)
 * (C) 2015 Project Hybrasyl (info@hybrasyl.com)
 *
 * Authors:   Justin Baugh  <baughj@hybrasyl.com>
 *            Kyle Speck    <kojasou@hybrasyl.com>
 */

using log4net;
using System;
using System.Text;

namespace Hybrasyl
{
    public abstract class Packet
    {
        public static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        protected static byte[][] SaltTable = new byte[][]
    {
      #region Seed 00
      new byte[] {
        0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F,
        0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F,
        0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x29, 0x2A, 0x2B, 0x2C, 0x2D, 0x2E, 0x2F,
        0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x3B, 0x3C, 0x3D, 0x3E, 0x3F,
        0x40, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49, 0x4A, 0x4B, 0x4C, 0x4D, 0x4E, 0x4F,
        0x50, 0x51, 0x52, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5A, 0x5B, 0x5C, 0x5D, 0x5E, 0x5F,
        0x60, 0x61, 0x62, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6A, 0x6B, 0x6C, 0x6D, 0x6E, 0x6F,
        0x70, 0x71, 0x72, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x7B, 0x7C, 0x7D, 0x7E, 0x7F,
        0x80, 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8A, 0x8B, 0x8C, 0x8D, 0x8E, 0x8F,
        0x90, 0x91, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9A, 0x9B, 0x9C, 0x9D, 0x9E, 0x9F,
        0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0xA6, 0xA7, 0xA8, 0xA9, 0xAA, 0xAB, 0xAC, 0xAD, 0xAE, 0xAF,
        0xB0, 0xB1, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA, 0xBB, 0xBC, 0xBD, 0xBE, 0xBF,
        0xC0, 0xC1, 0xC2, 0xC3, 0xC4, 0xC5, 0xC6, 0xC7, 0xC8, 0xC9, 0xCA, 0xCB, 0xCC, 0xCD, 0xCE, 0xCF,
        0xD0, 0xD1, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0xDA, 0xDB, 0xDC, 0xDD, 0xDE, 0xDF,
        0xE0, 0xE1, 0xE2, 0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xEB, 0xEC, 0xED, 0xEE, 0xEF,
        0xF0, 0xF1, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7, 0xF8, 0xF9, 0xFA, 0xFB, 0xFC, 0xFD, 0xFE, 0xFF
      },
      #endregion
      #region Seed 01
      new byte[] {
        0x80, 0x7F, 0x81, 0x7E, 0x82, 0x7D, 0x83, 0x7C, 0x84, 0x7B, 0x85, 0x7A, 0x86, 0x79, 0x87, 0x78,
        0x88, 0x77, 0x89, 0x76, 0x8A, 0x75, 0x8B, 0x74, 0x8C, 0x73, 0x8D, 0x72, 0x8E, 0x71, 0x8F, 0x70,
        0x90, 0x6F, 0x91, 0x6E, 0x92, 0x6D, 0x93, 0x6C, 0x94, 0x6B, 0x95, 0x6A, 0x96, 0x69, 0x97, 0x68,
        0x98, 0x67, 0x99, 0x66, 0x9A, 0x65, 0x9B, 0x64, 0x9C, 0x63, 0x9D, 0x62, 0x9E, 0x61, 0x9F, 0x60,
        0xA0, 0x5F, 0xA1, 0x5E, 0xA2, 0x5D, 0xA3, 0x5C, 0xA4, 0x5B, 0xA5, 0x5A, 0xA6, 0x59, 0xA7, 0x58,
        0xA8, 0x57, 0xA9, 0x56, 0xAA, 0x55, 0xAB, 0x54, 0xAC, 0x53, 0xAD, 0x52, 0xAE, 0x51, 0xAF, 0x50,
        0xB0, 0x4F, 0xB1, 0x4E, 0xB2, 0x4D, 0xB3, 0x4C, 0xB4, 0x4B, 0xB5, 0x4A, 0xB6, 0x49, 0xB7, 0x48,
        0xB8, 0x47, 0xB9, 0x46, 0xBA, 0x45, 0xBB, 0x44, 0xBC, 0x43, 0xBD, 0x42, 0xBE, 0x41, 0xBF, 0x40,
        0xC0, 0x3F, 0xC1, 0x3E, 0xC2, 0x3D, 0xC3, 0x3C, 0xC4, 0x3B, 0xC5, 0x3A, 0xC6, 0x39, 0xC7, 0x38,
        0xC8, 0x37, 0xC9, 0x36, 0xCA, 0x35, 0xCB, 0x34, 0xCC, 0x33, 0xCD, 0x32, 0xCE, 0x31, 0xCF, 0x30,
        0xD0, 0x2F, 0xD1, 0x2E, 0xD2, 0x2D, 0xD3, 0x2C, 0xD4, 0x2B, 0xD5, 0x2A, 0xD6, 0x29, 0xD7, 0x28,
        0xD8, 0x27, 0xD9, 0x26, 0xDA, 0x25, 0xDB, 0x24, 0xDC, 0x23, 0xDD, 0x22, 0xDE, 0x21, 0xDF, 0x20,
        0xE0, 0x1F, 0xE1, 0x1E, 0xE2, 0x1D, 0xE3, 0x1C, 0xE4, 0x1B, 0xE5, 0x1A, 0xE6, 0x19, 0xE7, 0x18,
        0xE8, 0x17, 0xE9, 0x16, 0xEA, 0x15, 0xEB, 0x14, 0xEC, 0x13, 0xED, 0x12, 0xEE, 0x11, 0xEF, 0x10,
        0xF0, 0x0F, 0xF1, 0x0E, 0xF2, 0x0D, 0xF3, 0x0C, 0xF4, 0x0B, 0xF5, 0x0A, 0xF6, 0x09, 0xF7, 0x08,
        0xF8, 0x07, 0xF9, 0x06, 0xFA, 0x05, 0xFB, 0x04, 0xFC, 0x03, 0xFD, 0x02, 0xFE, 0x01, 0xFF, 0x00
      },
      #endregion
      #region Seed 02
      new byte[] {
        0xFF, 0xFE, 0xFD, 0xFC, 0xFB, 0xFA, 0xF9, 0xF8, 0xF7, 0xF6, 0xF5, 0xF4, 0xF3, 0xF2, 0xF1, 0xF0,
        0xEF, 0xEE, 0xED, 0xEC, 0xEB, 0xEA, 0xE9, 0xE8, 0xE7, 0xE6, 0xE5, 0xE4, 0xE3, 0xE2, 0xE1, 0xE0,
        0xDF, 0xDE, 0xDD, 0xDC, 0xDB, 0xDA, 0xD9, 0xD8, 0xD7, 0xD6, 0xD5, 0xD4, 0xD3, 0xD2, 0xD1, 0xD0,
        0xCF, 0xCE, 0xCD, 0xCC, 0xCB, 0xCA, 0xC9, 0xC8, 0xC7, 0xC6, 0xC5, 0xC4, 0xC3, 0xC2, 0xC1, 0xC0,
        0xBF, 0xBE, 0xBD, 0xBC, 0xBB, 0xBA, 0xB9, 0xB8, 0xB7, 0xB6, 0xB5, 0xB4, 0xB3, 0xB2, 0xB1, 0xB0,
        0xAF, 0xAE, 0xAD, 0xAC, 0xAB, 0xAA, 0xA9, 0xA8, 0xA7, 0xA6, 0xA5, 0xA4, 0xA3, 0xA2, 0xA1, 0xA0,
        0x9F, 0x9E, 0x9D, 0x9C, 0x9B, 0x9A, 0x99, 0x98, 0x97, 0x96, 0x95, 0x94, 0x93, 0x92, 0x91, 0x90,
        0x8F, 0x8E, 0x8D, 0x8C, 0x8B, 0x8A, 0x89, 0x88, 0x87, 0x86, 0x85, 0x84, 0x83, 0x82, 0x81, 0x80,
        0x7F, 0x7E, 0x7D, 0x7C, 0x7B, 0x7A, 0x79, 0x78, 0x77, 0x76, 0x75, 0x74, 0x73, 0x72, 0x71, 0x70,
        0x6F, 0x6E, 0x6D, 0x6C, 0x6B, 0x6A, 0x69, 0x68, 0x67, 0x66, 0x65, 0x64, 0x63, 0x62, 0x61, 0x60,
        0x5F, 0x5E, 0x5D, 0x5C, 0x5B, 0x5A, 0x59, 0x58, 0x57, 0x56, 0x55, 0x54, 0x53, 0x52, 0x51, 0x50,
        0x4F, 0x4E, 0x4D, 0x4C, 0x4B, 0x4A, 0x49, 0x48, 0x47, 0x46, 0x45, 0x44, 0x43, 0x42, 0x41, 0x40,
        0x3F, 0x3E, 0x3D, 0x3C, 0x3B, 0x3A, 0x39, 0x38, 0x37, 0x36, 0x35, 0x34, 0x33, 0x32, 0x31, 0x30,
        0x2F, 0x2E, 0x2D, 0x2C, 0x2B, 0x2A, 0x29, 0x28, 0x27, 0x26, 0x25, 0x24, 0x23, 0x22, 0x21, 0x20,
        0x1F, 0x1E, 0x1D, 0x1C, 0x1B, 0x1A, 0x19, 0x18, 0x17, 0x16, 0x15, 0x14, 0x13, 0x12, 0x11, 0x10,
        0x0F, 0x0E, 0x0D, 0x0C, 0x0B, 0x0A, 0x09, 0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01, 0x00
      },
      #endregion
      #region Seed 03
      new byte[] {
        0xFF, 0x01, 0xFE, 0x02, 0xFD, 0x03, 0xFC, 0x04, 0xFB, 0x05, 0xFA, 0x06, 0xF9, 0x07, 0xF8, 0x08,
        0xF7, 0x09, 0xF6, 0x0A, 0xF5, 0x0B, 0xF4, 0x0C, 0xF3, 0x0D, 0xF2, 0x0E, 0xF1, 0x0F, 0xF0, 0x10,
        0xEF, 0x11, 0xEE, 0x12, 0xED, 0x13, 0xEC, 0x14, 0xEB, 0x15, 0xEA, 0x16, 0xE9, 0x17, 0xE8, 0x18,
        0xE7, 0x19, 0xE6, 0x1A, 0xE5, 0x1B, 0xE4, 0x1C, 0xE3, 0x1D, 0xE2, 0x1E, 0xE1, 0x1F, 0xE0, 0x20,
        0xDF, 0x21, 0xDE, 0x22, 0xDD, 0x23, 0xDC, 0x24, 0xDB, 0x25, 0xDA, 0x26, 0xD9, 0x27, 0xD8, 0x28,
        0xD7, 0x29, 0xD6, 0x2A, 0xD5, 0x2B, 0xD4, 0x2C, 0xD3, 0x2D, 0xD2, 0x2E, 0xD1, 0x2F, 0xD0, 0x30,
        0xCF, 0x31, 0xCE, 0x32, 0xCD, 0x33, 0xCC, 0x34, 0xCB, 0x35, 0xCA, 0x36, 0xC9, 0x37, 0xC8, 0x38,
        0xC7, 0x39, 0xC6, 0x3A, 0xC5, 0x3B, 0xC4, 0x3C, 0xC3, 0x3D, 0xC2, 0x3E, 0xC1, 0x3F, 0xC0, 0x40,
        0xBF, 0x41, 0xBE, 0x42, 0xBD, 0x43, 0xBC, 0x44, 0xBB, 0x45, 0xBA, 0x46, 0xB9, 0x47, 0xB8, 0x48,
        0xB7, 0x49, 0xB6, 0x4A, 0xB5, 0x4B, 0xB4, 0x4C, 0xB3, 0x4D, 0xB2, 0x4E, 0xB1, 0x4F, 0xB0, 0x50,
        0xAF, 0x51, 0xAE, 0x52, 0xAD, 0x53, 0xAC, 0x54, 0xAB, 0x55, 0xAA, 0x56, 0xA9, 0x57, 0xA8, 0x58,
        0xA7, 0x59, 0xA6, 0x5A, 0xA5, 0x5B, 0xA4, 0x5C, 0xA3, 0x5D, 0xA2, 0x5E, 0xA1, 0x5F, 0xA0, 0x60,
        0x9F, 0x61, 0x9E, 0x62, 0x9D, 0x63, 0x9C, 0x64, 0x9B, 0x65, 0x9A, 0x66, 0x99, 0x67, 0x98, 0x68,
        0x97, 0x69, 0x96, 0x6A, 0x95, 0x6B, 0x94, 0x6C, 0x93, 0x6D, 0x92, 0x6E, 0x91, 0x6F, 0x90, 0x70,
        0x8F, 0x71, 0x8E, 0x72, 0x8D, 0x73, 0x8C, 0x74, 0x8B, 0x75, 0x8A, 0x76, 0x89, 0x77, 0x88, 0x78,
        0x87, 0x79, 0x86, 0x7A, 0x85, 0x7B, 0x84, 0x7C, 0x83, 0x7D, 0x82, 0x7E, 0x81, 0x7F, 0x80, 0x80
      },
      #endregion
      #region Seed 04
      new byte[] {
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01,
        0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04,
        0x09, 0x09, 0x09, 0x09, 0x09, 0x09, 0x09, 0x09, 0x09, 0x09, 0x09, 0x09, 0x09, 0x09, 0x09, 0x09,
        0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10,
        0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19,
        0x24, 0x24, 0x24, 0x24, 0x24, 0x24, 0x24, 0x24, 0x24, 0x24, 0x24, 0x24, 0x24, 0x24, 0x24, 0x24,
        0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31,
        0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40,
        0x51, 0x51, 0x51, 0x51, 0x51, 0x51, 0x51, 0x51, 0x51, 0x51, 0x51, 0x51, 0x51, 0x51, 0x51, 0x51,
        0x64, 0x64, 0x64, 0x64, 0x64, 0x64, 0x64, 0x64, 0x64, 0x64, 0x64, 0x64, 0x64, 0x64, 0x64, 0x64,
        0x79, 0x79, 0x79, 0x79, 0x79, 0x79, 0x79, 0x79, 0x79, 0x79, 0x79, 0x79, 0x79, 0x79, 0x79, 0x79,
        0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90,
        0xA9, 0xA9, 0xA9, 0xA9, 0xA9, 0xA9, 0xA9, 0xA9, 0xA9, 0xA9, 0xA9, 0xA9, 0xA9, 0xA9, 0xA9, 0xA9,
        0xC4, 0xC4, 0xC4, 0xC4, 0xC4, 0xC4, 0xC4, 0xC4, 0xC4, 0xC4, 0xC4, 0xC4, 0xC4, 0xC4, 0xC4, 0xC4,
        0xE1, 0xE1, 0xE1, 0xE1, 0xE1, 0xE1, 0xE1, 0xE1, 0xE1, 0xE1, 0xE1, 0xE1, 0xE1, 0xE1, 0xE1, 0xE1
      },
      #endregion
      #region Seed 05
      new byte[] {
        0x00, 0x02, 0x04, 0x06, 0x08, 0x0A, 0x0C, 0x0E, 0x10, 0x12, 0x14, 0x16, 0x18, 0x1A, 0x1C, 0x1E,
        0x20, 0x22, 0x24, 0x26, 0x28, 0x2A, 0x2C, 0x2E, 0x30, 0x32, 0x34, 0x36, 0x38, 0x3A, 0x3C, 0x3E,
        0x40, 0x42, 0x44, 0x46, 0x48, 0x4A, 0x4C, 0x4E, 0x50, 0x52, 0x54, 0x56, 0x58, 0x5A, 0x5C, 0x5E,
        0x60, 0x62, 0x64, 0x66, 0x68, 0x6A, 0x6C, 0x6E, 0x70, 0x72, 0x74, 0x76, 0x78, 0x7A, 0x7C, 0x7E,
        0x80, 0x82, 0x84, 0x86, 0x88, 0x8A, 0x8C, 0x8E, 0x90, 0x92, 0x94, 0x96, 0x98, 0x9A, 0x9C, 0x9E,
        0xA0, 0xA2, 0xA4, 0xA6, 0xA8, 0xAA, 0xAC, 0xAE, 0xB0, 0xB2, 0xB4, 0xB6, 0xB8, 0xBA, 0xBC, 0xBE,
        0xC0, 0xC2, 0xC4, 0xC6, 0xC8, 0xCA, 0xCC, 0xCE, 0xD0, 0xD2, 0xD4, 0xD6, 0xD8, 0xDA, 0xDC, 0xDE,
        0xE0, 0xE2, 0xE4, 0xE6, 0xE8, 0xEA, 0xEC, 0xEE, 0xF0, 0xF2, 0xF4, 0xF6, 0xF8, 0xFA, 0xFC, 0xFE,
        0x00, 0x02, 0x04, 0x06, 0x08, 0x0A, 0x0C, 0x0E, 0x10, 0x12, 0x14, 0x16, 0x18, 0x1A, 0x1C, 0x1E,
        0x20, 0x22, 0x24, 0x26, 0x28, 0x2A, 0x2C, 0x2E, 0x30, 0x32, 0x34, 0x36, 0x38, 0x3A, 0x3C, 0x3E,
        0x40, 0x42, 0x44, 0x46, 0x48, 0x4A, 0x4C, 0x4E, 0x50, 0x52, 0x54, 0x56, 0x58, 0x5A, 0x5C, 0x5E,
        0x60, 0x62, 0x64, 0x66, 0x68, 0x6A, 0x6C, 0x6E, 0x70, 0x72, 0x74, 0x76, 0x78, 0x7A, 0x7C, 0x7E,
        0x80, 0x82, 0x84, 0x86, 0x88, 0x8A, 0x8C, 0x8E, 0x90, 0x92, 0x94, 0x96, 0x98, 0x9A, 0x9C, 0x9E,
        0xA0, 0xA2, 0xA4, 0xA6, 0xA8, 0xAA, 0xAC, 0xAE, 0xB0, 0xB2, 0xB4, 0xB6, 0xB8, 0xBA, 0xBC, 0xBE,
        0xC0, 0xC2, 0xC4, 0xC6, 0xC8, 0xCA, 0xCC, 0xCE, 0xD0, 0xD2, 0xD4, 0xD6, 0xD8, 0xDA, 0xDC, 0xDE,
        0xE0, 0xE2, 0xE4, 0xE6, 0xE8, 0xEA, 0xEC, 0xEE, 0xF0, 0xF2, 0xF4, 0xF6, 0xF8, 0xFA, 0xFC, 0xFE
      },
      #endregion
      #region Seed 06
      new byte[] {
        0xFF, 0xFD, 0xFB, 0xF9, 0xF7, 0xF5, 0xF3, 0xF1, 0xEF, 0xED, 0xEB, 0xE9, 0xE7, 0xE5, 0xE3, 0xE1,
        0xDF, 0xDD, 0xDB, 0xD9, 0xD7, 0xD5, 0xD3, 0xD1, 0xCF, 0xCD, 0xCB, 0xC9, 0xC7, 0xC5, 0xC3, 0xC1,
        0xBF, 0xBD, 0xBB, 0xB9, 0xB7, 0xB5, 0xB3, 0xB1, 0xAF, 0xAD, 0xAB, 0xA9, 0xA7, 0xA5, 0xA3, 0xA1,
        0x9F, 0x9D, 0x9B, 0x99, 0x97, 0x95, 0x93, 0x91, 0x8F, 0x8D, 0x8B, 0x89, 0x87, 0x85, 0x83, 0x81,
        0x7F, 0x7D, 0x7B, 0x79, 0x77, 0x75, 0x73, 0x71, 0x6F, 0x6D, 0x6B, 0x69, 0x67, 0x65, 0x63, 0x61,
        0x5F, 0x5D, 0x5B, 0x59, 0x57, 0x55, 0x53, 0x51, 0x4F, 0x4D, 0x4B, 0x49, 0x47, 0x45, 0x43, 0x41,
        0x3F, 0x3D, 0x3B, 0x39, 0x37, 0x35, 0x33, 0x31, 0x2F, 0x2D, 0x2B, 0x29, 0x27, 0x25, 0x23, 0x21,
        0x1F, 0x1D, 0x1B, 0x19, 0x17, 0x15, 0x13, 0x11, 0x0F, 0x0D, 0x0B, 0x09, 0x07, 0x05, 0x03, 0x01,
        0xFF, 0xFD, 0xFB, 0xF9, 0xF7, 0xF5, 0xF3, 0xF1, 0xEF, 0xED, 0xEB, 0xE9, 0xE7, 0xE5, 0xE3, 0xE1,
        0xDF, 0xDD, 0xDB, 0xD9, 0xD7, 0xD5, 0xD3, 0xD1, 0xCF, 0xCD, 0xCB, 0xC9, 0xC7, 0xC5, 0xC3, 0xC1,
        0xBF, 0xBD, 0xBB, 0xB9, 0xB7, 0xB5, 0xB3, 0xB1, 0xAF, 0xAD, 0xAB, 0xA9, 0xA7, 0xA5, 0xA3, 0xA1,
        0x9F, 0x9D, 0x9B, 0x99, 0x97, 0x95, 0x93, 0x91, 0x8F, 0x8D, 0x8B, 0x89, 0x87, 0x85, 0x83, 0x81,
        0x7F, 0x7D, 0x7B, 0x79, 0x77, 0x75, 0x73, 0x71, 0x6F, 0x6D, 0x6B, 0x69, 0x67, 0x65, 0x63, 0x61,
        0x5F, 0x5D, 0x5B, 0x59, 0x57, 0x55, 0x53, 0x51, 0x4F, 0x4D, 0x4B, 0x49, 0x47, 0x45, 0x43, 0x41,
        0x3F, 0x3D, 0x3B, 0x39, 0x37, 0x35, 0x33, 0x31, 0x2F, 0x2D, 0x2B, 0x29, 0x27, 0x25, 0x23, 0x21,
        0x1F, 0x1D, 0x1B, 0x19, 0x17, 0x15, 0x13, 0x11, 0x0F, 0x0D, 0x0B, 0x09, 0x07, 0x05, 0x03, 0x01
      },
      #endregion
      #region Seed 07
      new byte[] {
        0xFF, 0xFD, 0xFB, 0xF9, 0xF7, 0xF5, 0xF3, 0xF1, 0xEF, 0xED, 0xEB, 0xE9, 0xE7, 0xE5, 0xE3, 0xE1,
        0xDF, 0xDD, 0xDB, 0xD9, 0xD7, 0xD5, 0xD3, 0xD1, 0xCF, 0xCD, 0xCB, 0xC9, 0xC7, 0xC5, 0xC3, 0xC1,
        0xBF, 0xBD, 0xBB, 0xB9, 0xB7, 0xB5, 0xB3, 0xB1, 0xAF, 0xAD, 0xAB, 0xA9, 0xA7, 0xA5, 0xA3, 0xA1,
        0x9F, 0x9D, 0x9B, 0x99, 0x97, 0x95, 0x93, 0x91, 0x8F, 0x8D, 0x8B, 0x89, 0x87, 0x85, 0x83, 0x81,
        0x7F, 0x7D, 0x7B, 0x79, 0x77, 0x75, 0x73, 0x71, 0x6F, 0x6D, 0x6B, 0x69, 0x67, 0x65, 0x63, 0x61,
        0x5F, 0x5D, 0x5B, 0x59, 0x57, 0x55, 0x53, 0x51, 0x4F, 0x4D, 0x4B, 0x49, 0x47, 0x45, 0x43, 0x41,
        0x3F, 0x3D, 0x3B, 0x39, 0x37, 0x35, 0x33, 0x31, 0x2F, 0x2D, 0x2B, 0x29, 0x27, 0x25, 0x23, 0x21,
        0x1F, 0x1D, 0x1B, 0x19, 0x17, 0x15, 0x13, 0x11, 0x0F, 0x0D, 0x0B, 0x09, 0x07, 0x05, 0x03, 0x01,
        0x00, 0x02, 0x04, 0x06, 0x08, 0x0A, 0x0C, 0x0E, 0x10, 0x12, 0x14, 0x16, 0x18, 0x1A, 0x1C, 0x1E,
        0x20, 0x22, 0x24, 0x26, 0x28, 0x2A, 0x2C, 0x2E, 0x30, 0x32, 0x34, 0x36, 0x38, 0x3A, 0x3C, 0x3E,
        0x40, 0x42, 0x44, 0x46, 0x48, 0x4A, 0x4C, 0x4E, 0x50, 0x52, 0x54, 0x56, 0x58, 0x5A, 0x5C, 0x5E,
        0x60, 0x62, 0x64, 0x66, 0x68, 0x6A, 0x6C, 0x6E, 0x70, 0x72, 0x74, 0x76, 0x78, 0x7A, 0x7C, 0x7E,
        0x80, 0x82, 0x84, 0x86, 0x88, 0x8A, 0x8C, 0x8E, 0x90, 0x92, 0x94, 0x96, 0x98, 0x9A, 0x9C, 0x9E,
        0xA0, 0xA2, 0xA4, 0xA6, 0xA8, 0xAA, 0xAC, 0xAE, 0xB0, 0xB2, 0xB4, 0xB6, 0xB8, 0xBA, 0xBC, 0xBE,
        0xC0, 0xC2, 0xC4, 0xC6, 0xC8, 0xCA, 0xCC, 0xCE, 0xD0, 0xD2, 0xD4, 0xD6, 0xD8, 0xDA, 0xDC, 0xDE,
        0xE0, 0xE2, 0xE4, 0xE6, 0xE8, 0xEA, 0xEC, 0xEE, 0xF0, 0xF2, 0xF4, 0xF6, 0xF8, 0xFA, 0xFC, 0xFE
      },
      #endregion
      #region Seed 08
      new byte[] {
        0x00, 0x02, 0x04, 0x06, 0x08, 0x0A, 0x0C, 0x0E, 0x10, 0x12, 0x14, 0x16, 0x18, 0x1A, 0x1C, 0x1E,
        0x20, 0x22, 0x24, 0x26, 0x28, 0x2A, 0x2C, 0x2E, 0x30, 0x32, 0x34, 0x36, 0x38, 0x3A, 0x3C, 0x3E,
        0x40, 0x42, 0x44, 0x46, 0x48, 0x4A, 0x4C, 0x4E, 0x50, 0x52, 0x54, 0x56, 0x58, 0x5A, 0x5C, 0x5E,
        0x60, 0x62, 0x64, 0x66, 0x68, 0x6A, 0x6C, 0x6E, 0x70, 0x72, 0x74, 0x76, 0x78, 0x7A, 0x7C, 0x7E,
        0x80, 0x82, 0x84, 0x86, 0x88, 0x8A, 0x8C, 0x8E, 0x90, 0x92, 0x94, 0x96, 0x98, 0x9A, 0x9C, 0x9E,
        0xA0, 0xA2, 0xA4, 0xA6, 0xA8, 0xAA, 0xAC, 0xAE, 0xB0, 0xB2, 0xB4, 0xB6, 0xB8, 0xBA, 0xBC, 0xBE,
        0xC0, 0xC2, 0xC4, 0xC6, 0xC8, 0xCA, 0xCC, 0xCE, 0xD0, 0xD2, 0xD4, 0xD6, 0xD8, 0xDA, 0xDC, 0xDE,
        0xE0, 0xE2, 0xE4, 0xE6, 0xE8, 0xEA, 0xEC, 0xEE, 0xF0, 0xF2, 0xF4, 0xF6, 0xF8, 0xFA, 0xFC, 0xFE,
        0xFF, 0xFD, 0xFB, 0xF9, 0xF7, 0xF5, 0xF3, 0xF1, 0xEF, 0xED, 0xEB, 0xE9, 0xE7, 0xE5, 0xE3, 0xE1,
        0xDF, 0xDD, 0xDB, 0xD9, 0xD7, 0xD5, 0xD3, 0xD1, 0xCF, 0xCD, 0xCB, 0xC9, 0xC7, 0xC5, 0xC3, 0xC1,
        0xBF, 0xBD, 0xBB, 0xB9, 0xB7, 0xB5, 0xB3, 0xB1, 0xAF, 0xAD, 0xAB, 0xA9, 0xA7, 0xA5, 0xA3, 0xA1,
        0x9F, 0x9D, 0x9B, 0x99, 0x97, 0x95, 0x93, 0x91, 0x8F, 0x8D, 0x8B, 0x89, 0x87, 0x85, 0x83, 0x81,
        0x7F, 0x7D, 0x7B, 0x79, 0x77, 0x75, 0x73, 0x71, 0x6F, 0x6D, 0x6B, 0x69, 0x67, 0x65, 0x63, 0x61,
        0x5F, 0x5D, 0x5B, 0x59, 0x57, 0x55, 0x53, 0x51, 0x4F, 0x4D, 0x4B, 0x49, 0x47, 0x45, 0x43, 0x41,
        0x3F, 0x3D, 0x3B, 0x39, 0x37, 0x35, 0x33, 0x31, 0x2F, 0x2D, 0x2B, 0x29, 0x27, 0x25, 0x23, 0x21,
        0x1F, 0x1D, 0x1B, 0x19, 0x17, 0x15, 0x13, 0x11, 0x0F, 0x0D, 0x0B, 0x09, 0x07, 0x05, 0x03, 0x01
      },
      #endregion
      #region Seed 09
      new byte[] {
        0xFF, 0x1E, 0x1E, 0x1E, 0x1E, 0x1E, 0x1E, 0x1E, 0x1E, 0x3B, 0x3B, 0x3B, 0x3B, 0x3B, 0x3B, 0x3B,
        0x3B, 0x56, 0x56, 0x56, 0x56, 0x56, 0x56, 0x56, 0x56, 0x6F, 0x6F, 0x6F, 0x6F, 0x6F, 0x6F, 0x6F,
        0x6F, 0x86, 0x86, 0x86, 0x86, 0x86, 0x86, 0x86, 0x86, 0x9B, 0x9B, 0x9B, 0x9B, 0x9B, 0x9B, 0x9B,
        0x9B, 0xAE, 0xAE, 0xAE, 0xAE, 0xAE, 0xAE, 0xAE, 0xAE, 0xBF, 0xBF, 0xBF, 0xBF, 0xBF, 0xBF, 0xBF,
        0xBF, 0xCE, 0xCE, 0xCE, 0xCE, 0xCE, 0xCE, 0xCE, 0xCE, 0xDB, 0xDB, 0xDB, 0xDB, 0xDB, 0xDB, 0xDB,
        0xDB, 0xE6, 0xE6, 0xE6, 0xE6, 0xE6, 0xE6, 0xE6, 0xE6, 0xEF, 0xEF, 0xEF, 0xEF, 0xEF, 0xEF, 0xEF,
        0xEF, 0xF6, 0xF6, 0xF6, 0xF6, 0xF6, 0xF6, 0xF6, 0xF6, 0xFB, 0xFB, 0xFB, 0xFB, 0xFB, 0xFB, 0xFB,
        0xFB, 0xFE, 0xFE, 0xFE, 0xFE, 0xFE, 0xFE, 0xFE, 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFE, 0xFE, 0xFE, 0xFE, 0xFE, 0xFE, 0xFE, 0xFE,
        0xFB, 0xFB, 0xFB, 0xFB, 0xFB, 0xFB, 0xFB, 0xFB, 0xF6, 0xF6, 0xF6, 0xF6, 0xF6, 0xF6, 0xF6, 0xF6,
        0xEF, 0xEF, 0xEF, 0xEF, 0xEF, 0xEF, 0xEF, 0xEF, 0xE6, 0xE6, 0xE6, 0xE6, 0xE6, 0xE6, 0xE6, 0xE6,
        0xDB, 0xDB, 0xDB, 0xDB, 0xDB, 0xDB, 0xDB, 0xDB, 0xCE, 0xCE, 0xCE, 0xCE, 0xCE, 0xCE, 0xCE, 0xCE,
        0xBF, 0xBF, 0xBF, 0xBF, 0xBF, 0xBF, 0xBF, 0xBF, 0xAE, 0xAE, 0xAE, 0xAE, 0xAE, 0xAE, 0xAE, 0xAE,
        0x9B, 0x9B, 0x9B, 0x9B, 0x9B, 0x9B, 0x9B, 0x9B, 0x86, 0x86, 0x86, 0x86, 0x86, 0x86, 0x86, 0x86,
        0x6F, 0x6F, 0x6F, 0x6F, 0x6F, 0x6F, 0x6F, 0x6F, 0x56, 0x56, 0x56, 0x56, 0x56, 0x56, 0x56, 0x56,
        0x3B, 0x3B, 0x3B, 0x3B, 0x3B, 0x3B, 0x3B, 0x3B, 0x1E, 0x1E, 0x1E, 0x1E, 0x1E, 0x1E, 0x1E, 0x1E
      }
      #endregion
    };

        public byte Opcode { get; protected set; }
        public byte Ordinal { get; set; }
        public int TransmitDelay { get; set; }

        protected byte[] Data;
        protected int _position;

        protected Packet()
        {
            TransmitDelay = 0;
        }

        public int Position
        {
            get { return _position; }
        }

        public abstract bool ShouldEncrypt { get; }
        public abstract bool UseDefaultKey { get; }

        public void DumpPacket()
        {
            // Dump the packet to the console.
            Logger.DebugFormat("Dumping packet:");
            Logger.DebugFormat(BitConverter.ToString(Data));
        }

        public byte[] ToArray()
        {
            var buffer = new byte[Data.Length + (ShouldEncrypt ? 5 : 4)];
            buffer[0] = 0xAA;
            buffer[1] = (byte)((buffer.Length - 3) / 256);
            buffer[2] = (byte)(buffer.Length - 3);
            buffer[3] = Opcode;
            buffer[4] = Ordinal;
            Array.Copy(Data, 0, buffer, ShouldEncrypt ? 5 : 4, Data.Length);
            return buffer;
        }
        public static explicit operator byte[](Packet packet)
        {
            return packet.ToArray();
        }
        public override string ToString()
        {
            return BitConverter.ToString(ToArray());
        }

        public int Seek(int offset, PacketSeekOrigin origin)
        {
            if (origin == PacketSeekOrigin.Begin) _position = 0;
            if (origin == PacketSeekOrigin.End) _position = Data.Length;
            _position += offset;
            if (_position < 0) _position = 0;
            if (_position > Data.Length) _position = Data.Length;
            return _position;
        }
    }

    public enum PacketSeekOrigin
    {
        Begin,
        Current,
        End
    }

    public class ClientPacket : Packet
    {
        #region Dialog Crc Table
        protected static ushort[] DialogCrcTable = new ushort[]
    {
      0x0000, 0x1021, 0x2042, 0x3063, 0x4084, 0x50A5, 0x60C6, 0x70E7,
      0x8108, 0x9129, 0xA14A, 0xB16B, 0xC18C, 0xD1AD, 0xE1CE, 0xF1EF,
      0x1231, 0x0210, 0x3273, 0x2252, 0x52B5, 0x4294, 0x72F7, 0x62D6,
      0x9339, 0x8318, 0xB37B, 0xA35A, 0xD3BD, 0xC39C, 0xF3FF, 0xE3DE,
      0x2462, 0x3443, 0x0420, 0x1401, 0x64E6, 0x74C7, 0x44A4, 0x5485,
      0xA56A, 0xB54B, 0x8528, 0x9509, 0xE5EE, 0xF5CF, 0xC5AC, 0xD58D,
      0x3653, 0x2672, 0x1611, 0x0630, 0x76D7, 0x66F6, 0x5695, 0x46B4,
      0xB75B, 0xA77A, 0x9719, 0x8738, 0xF7DF, 0xE7FE, 0xD79D, 0xC7BC,
      0x48C4, 0x58E5, 0x6886, 0x78A7, 0x0840, 0x1861, 0x2802, 0x3823,
      0xC9CC, 0xD9ED, 0xE98E, 0xF9AF, 0x8948, 0x9969, 0xA90A, 0xB92B,
      0x5AF5, 0x4AD4, 0x7AB7, 0x6A96, 0x1A71, 0x0A50, 0x3A33, 0x2A12,
      0xDBFD, 0xCBDC, 0xFBBF, 0xEB9E, 0x9B79, 0x8B58, 0xBB3B, 0xAB1A,
      0x6CA6, 0x7C87, 0x4CE4, 0x5CC5, 0x2C22, 0x3C03, 0x0C60, 0x1C41,
      0xEDAE, 0xFD8F, 0xCDEC, 0xDDCD, 0xAD2A, 0xBD0B, 0x8D68, 0x9D49,
      0x7E97, 0x6EB6, 0x5ED5, 0x4EF4, 0x3E13, 0x2E32, 0x1E51, 0x0E70,
      0xFF9F, 0xEFBE, 0xDFDD, 0xCFFC, 0xBF1B, 0xAF3A, 0x9F59, 0x8F78,
      0x9188, 0x81A9, 0xB1CA, 0xA1EB, 0xD10C, 0xC12D, 0xF14E, 0xE16F,
      0x1080, 0x00A1, 0x30C2, 0x20E3, 0x5004, 0x4025, 0x7046, 0x6067,
      0x83B9, 0x9398, 0xA3FB, 0xB3DA, 0xC33D, 0xD31C, 0xE37F, 0xF35E,
      0x02B1, 0x1290, 0x22F3, 0x32D2, 0x4235, 0x5214, 0x6277, 0x7256,
      0xB5EA, 0xA5CB, 0x95A8, 0x8589, 0xF56E, 0xE54F, 0xD52C, 0xC50D,
      0x34E2, 0x24C3, 0x14A0, 0x0481, 0x7466, 0x6447, 0x5424, 0x4405,
      0xA7DB, 0xB7FA, 0x8799, 0x97B8, 0xE75F, 0xF77E, 0xC71D, 0xD73C,
      0x26D3, 0x36F2, 0x0691, 0x16B0, 0x6657, 0x7676, 0x4615, 0x5634,
      0xD94C, 0xC96D, 0xF90E, 0xE92F, 0x99C8, 0x89E9, 0xB98A, 0xA9AB,
      0x5844, 0x4865, 0x7806, 0x6827, 0x18C0, 0x08E1, 0x3882, 0x28A3,
      0xCB7D, 0xDB5C, 0xEB3F, 0xFB1E, 0x8BF9, 0x9BD8, 0xABBB, 0xBB9A,
      0x4A75, 0x5A54, 0x6A37, 0x7A16, 0x0AF1, 0x1AD0, 0x2AB3, 0x3A92,
      0xFD2E, 0xED0F, 0xDD6C, 0xCD4D, 0xBDAA, 0xAD8B, 0x9DE8, 0x8DC9,
      0x7C26, 0x6C07, 0x5C64, 0x4C45, 0x3CA2, 0x2C83, 0x1CE0, 0x0CC1,
      0xEF1F, 0xFF3E, 0xCF5D, 0xDF7C, 0xAF9B, 0xBFBA, 0x8FD9, 0x9FF8,
      0x6E17, 0x7E36, 0x4E55, 0x5E74, 0x2E93, 0x3EB2, 0x0ED1, 0x1EF0
    };
        #endregion

        public override bool ShouldEncrypt
        {
            get
            {
                return Opcode != 0x00 && Opcode != 0x10;
            }
        }
        public override bool UseDefaultKey
        {
            get
            {
                return Opcode == 0x02 || Opcode == 0x03 || Opcode == 0x04 || Opcode == 0x0B || Opcode == 0x26
                    || Opcode == 0x2D || Opcode == 0x3A || Opcode == 0x42 || Opcode == 0x43 || Opcode == 0x4B
                    || Opcode == 0x57 || Opcode == 0x62 || Opcode == 0x68 || Opcode == 0x71 || Opcode == 0x73
                    || Opcode == 0x7B;
            }
        }

        public ClientPacket(byte[] buffer)
        {
            Opcode = buffer[3];
            if (ShouldEncrypt)
            {
                Ordinal = buffer[4];
                Data = new byte[buffer.Length - 5];
                Array.Copy(buffer, 5, Data, 0, Data.Length);
            }
            else
            {
                Data = new byte[buffer.Length - 4];
                Array.Copy(buffer, 4, Data, 0, Data.Length);
            }
        }

        public byte[] Read(int length)
        {
            if (_position + length > Data.Length)
                throw new IndexOutOfRangeException();

            var buffer = new byte[length];
            Array.Copy(Data, _position, buffer, 0, length);

            _position += length;

            return buffer;
        }

        public byte[] ReadDialogHeader()
        {
            return Read(6); // Read six bytes
        }
        public byte ReadByte()
        {
            if (_position + 1 > Data.Length)
                throw new IndexOutOfRangeException();

            return Data[_position++];
        }
        public sbyte ReadSByte()
        {
            if (_position + 1 > Data.Length)
                throw new IndexOutOfRangeException();

            return (sbyte)Data[_position++];
        }
        public bool ReadBoolean()
        {
            if (_position + 1 > Data.Length)
                throw new IndexOutOfRangeException();

            return Data[_position++] != 0;
        }

        public short ReadInt16()
        {
            if (_position + 2 > Data.Length)
                throw new IndexOutOfRangeException();

            return (short)(Data[_position++] << 8 | Data[_position++]);
        }
        public ushort ReadUInt16()
        {
            if (_position + 2 > Data.Length)
                throw new IndexOutOfRangeException();

            return (ushort)(Data[_position++] << 8 | Data[_position++]);
        }

        public int ReadInt32()
        {
            if (_position + 4 > Data.Length)
                throw new IndexOutOfRangeException();

            return (int)(Data[_position++] << 24 | Data[_position++] << 16 | Data[_position++] << 8 | Data[_position++]);
        }
        public uint ReadUInt32()
        {
            if (_position + 4 > Data.Length)
                throw new IndexOutOfRangeException();

            return (uint)(Data[_position++] << 24 | Data[_position++] << 16 | Data[_position++] << 8 | Data[_position++]);
        }

        public string ReadString8()
        {
            if (_position + 1 > Data.Length)
                throw new IndexOutOfRangeException();

            var length = Data[_position];

            if (_position + 1 + length > Data.Length)
                throw new IndexOutOfRangeException();

            var buffer = new byte[length];
            Array.Copy(Data, _position + 1, buffer, 0, length);

            _position += length + 1;

            return Encoding.GetEncoding(949).GetString(buffer);
        }
        public string ReadString16()
        {
            if (_position + 1 > Data.Length)
                throw new IndexOutOfRangeException();

            var length = Data[_position] << 8 | Data[_position + 1];

            if (_position + 2 + length > Data.Length)
                throw new IndexOutOfRangeException();

            var buffer = new byte[length];
            Array.Copy(Data, _position + 2, buffer, 0, length);

            _position += length + 2;

            return Encoding.GetEncoding(949).GetString(buffer);
        }

        public void GenerateDialogHeader()
        {
            ushort crc = 0;
            for (var i = 0; i < Data.Length - 6; i++)
            {
                crc = (ushort)(Data[6 + i] ^ ((ushort)(crc << 8) ^ (ushort)DialogCrcTable[(crc >> 8)]));
            }
            var rand = new Random();
            Data[0] = (byte)rand.Next();
            Data[1] = (byte)rand.Next();
            Data[2] = (byte)((Data.Length - 4) / 256);
            Data[3] = (byte)((Data.Length - 4) % 256);
            Data[4] = (byte)(crc / 256);
            Data[5] = (byte)(crc % 256);
        }
        public void EncryptDialog()
        {
            var length = Data[2] << 8 | Data[3];
            byte xPrime = (byte)(Data[0] - 0x2D);
            byte x = (byte)(Data[1] ^ xPrime);
            byte y = (byte)(x + 0x72);
            byte z = (byte)(x + 0x28);
            Data[2] ^= y;
            Data[3] ^= (byte)((y + 1) % 256);
            for (int i = 0; i < length; i++)
            {
                Data[4 + i] ^= (byte)((z + i) % 256);
            }
        }
        public void DecryptDialog()
        {
            byte xPrime = (byte)(Data[0] - 0x2D);
            byte x = (byte)(Data[1] ^ xPrime);
            byte y = (byte)(x + 0x72);
            byte z = (byte)(x + 0x28);
            Data[2] ^= y;
            Data[3] ^= (byte)((y + 1) % 256);
            var length = Data[2] << 8 | Data[3];
            for (int i = 0; i < length; i++)
            {
                Data[4 + i] ^= (byte)((z + i) % 256);
            }
        }
        public void Decrypt(Client client)
        {
            var length = Data.Length - 3;

            var bRand = (ushort)((Data[length + 2] << 8 | Data[length]) ^ 0x7470);
            var sRand = (byte)(Data[length + 1] ^ 0x23);

            var key = (UseDefaultKey) ? client.EncryptionKey : client.GenerateKey(bRand, sRand);

            for (var i = 0; i < length; i++)
            {
                Data[i] ^= key[i % key.Length];
                Data[i] ^= SaltTable[client.EncryptionSeed][(i / key.Length) % SaltTable[client.EncryptionSeed].Length];
                if ((i / key.Length) % SaltTable[client.EncryptionSeed].Length != Ordinal)
                {
                    Data[i] ^= SaltTable[client.EncryptionSeed][Ordinal];
                }
            }
        }
    }

    public class ServerPacket : Packet
    {
        public override bool ShouldEncrypt
        {
            get
            {
                return Opcode != 0x00 && Opcode != 0x03 && Opcode != 0x7E;
            }
        }
        public override bool UseDefaultKey
        {
            get
            {
                return Opcode == 0x01 || Opcode == 0x02 || Opcode == 0x0A || Opcode == 0x56 || Opcode == 0x60
                    || Opcode == 0x62 || Opcode == 0x66 || Opcode == 0x6F;
            }
        }

        public ServerPacket(byte opcode)
        {
            Opcode = opcode;
            Data = new byte[0];
        }

        public void Write(byte[] buffer)
        {
            if (_position + buffer.Length > Data.Length)
            {
                Array.Resize(ref Data, _position + buffer.Length);
            }
            Array.Copy(buffer, 0, Data, _position, buffer.Length);
            _position += buffer.Length;
        }

        public void WriteByte(byte value)
        {
            if (_position + 1 > Data.Length)
            {
                Array.Resize(ref Data, _position + 1);
            }
            Data[_position++] = value;
        }
        public void WriteSByte(sbyte value)
        {
            if (_position + 1 > Data.Length)
            {
                Array.Resize(ref Data, _position + 1);
            }
            Data[_position++] = (byte)value;
        }
        public void WriteBoolean(bool value)
        {
            if (_position + 1 > Data.Length)
            {
                Array.Resize(ref Data, _position + 1);
            }
            Data[_position++] = (byte)(value ? 1 : 0);
        }

        public void WriteInt16(short value)
        {
            if (_position + 2 > Data.Length)
            {
                Array.Resize(ref Data, _position + 2);
            }
            Data[_position++] = (byte)(value >> 8);
            Data[_position++] = (byte)value;
        }
        public void WriteUInt16(ushort value)
        {
            if (_position + 2 > Data.Length)
            {
                Array.Resize(ref Data, _position + 2);
            }
            Data[_position++] = (byte)(value >> 8);
            Data[_position++] = (byte)value;
        }

        public void WriteInt32(int value)
        {
            if (_position + 4 > Data.Length)
            {
                Array.Resize(ref Data, _position + 4);
            }
            Data[_position++] = (byte)(value >> 24);
            Data[_position++] = (byte)(value >> 16);
            Data[_position++] = (byte)(value >> 8);
            Data[_position++] = (byte)value;
        }
        public void WriteUInt32(uint value)
        {
            if (_position + 4 > Data.Length)
            {
                Array.Resize(ref Data, _position + 4);
            }
            Data[_position++] = (byte)(value >> 24);
            Data[_position++] = (byte)(value >> 16);
            Data[_position++] = (byte)(value >> 8);
            Data[_position++] = (byte)value;
        }

        public void WriteStringWithLength(string value)
        {
            WriteByte((byte)value.Length);
            var buffer = Encoding.GetEncoding(949).GetBytes(value);
            if (_position + buffer.Length > Data.Length)
            {
                Array.Resize(ref Data, _position + buffer.Length);
            }
            Array.Copy(buffer, 0, Data, _position, buffer.Length);
            _position += buffer.Length;
        }

        public void WriteString(string value)
        {
            var buffer = Encoding.GetEncoding(949).GetBytes(value);
            if (_position + buffer.Length > Data.Length)
            {
                Array.Resize(ref Data, _position + buffer.Length);
            }
            Array.Copy(buffer, 0, Data, _position, buffer.Length);
            _position += buffer.Length;
        }
        public void WriteString8(string value)
        {
            value = value == null ? String.Empty : value;
            var buffer = Encoding.GetEncoding(949).GetBytes(value);
            if (_position + 1 + buffer.Length > Data.Length)
            {
                Array.Resize(ref Data, _position + 1 + buffer.Length);
            }
            Data[_position++] = (byte)buffer.Length;
            Array.Copy(buffer, 0, Data, _position, buffer.Length);
            _position += buffer.Length;
        }
        public void WriteString16(string value)
        {
            var buffer = Encoding.GetEncoding(949).GetBytes(value);
            if (_position + 2 + buffer.Length > Data.Length)
            {
                Array.Resize(ref Data, _position + 2 + buffer.Length);
            }
            Data[_position++] = (byte)(buffer.Length >> 8);
            Data[_position++] = (byte)buffer.Length;
            Array.Copy(buffer, 0, Data, _position, buffer.Length);
            _position += buffer.Length;
        }

        public void GenerateFooter()
        {
            int length = Data.Length;

            if (UseDefaultKey)
            {
                Array.Resize(ref Data, length + 1);
                Data[length++] = 0x00;
            }
            else
            {
                Array.Resize(ref Data, length + 2);
                Data[length++] = 0x00;
                Data[length++] = Opcode;
            }

            Array.Resize(ref Data, length + 3);
        }
        public void Encrypt(Client client)
        {
            var length = Data.Length - 3;

            var rand = new Random();
            var bRand = (ushort)(rand.Next() % 65277 + 256);
            var sRand = (byte)(rand.Next() % 155 + 100);

            var key = (UseDefaultKey) ? client.EncryptionKey : client.GenerateKey(bRand, sRand);

            for (var i = 0; i < length; i++)
            {
                Data[i] ^= key[i % key.Length];
                Data[i] ^= SaltTable[client.EncryptionSeed][(i / key.Length) % SaltTable[client.EncryptionSeed].Length];
                if ((i / key.Length) % SaltTable[client.EncryptionSeed].Length != Ordinal)
                {
                    Data[i] ^= SaltTable[client.EncryptionSeed][Ordinal];
                }
            }

            Data[length] = (byte)(bRand % 256 ^ 0x74);
            Data[length + 1] = (byte)(sRand ^ 0x24);
            Data[length + 2] = (byte)((bRand >> 8) % 256 ^ 0x64);
        }
    }
}
