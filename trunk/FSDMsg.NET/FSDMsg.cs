/* 
 * Portions copyright (C) 2008 On-Line Strategies, Inc.
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *    http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
 
/* Based upon FSDMsg.java r2539 from www.jpos.org
 * 
 * Orginally written by:
 *   Alejandro Revilla
 *   Mark Salter
 * Modified to C#.net:
 *   David Bergert (bergert@olsdallas.com)
 * 
 */

/* 
 * Copyright (c) 2005 jPOS.org.  All rights reserved.
 *
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 *
 * 1. Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 *
 * 2. Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in
 *    the documentation and/or other materials provided with the
 *    distribution.
 *
 * 3. The end-user documentation included with the redistribution,
 *    if any, must include the following acknowledgment:
 *    "This product includes software developed by the jPOS project 
 *    (http://www.jpos.org/)". Alternately, this acknowledgment may 
 *    appear in the software itself, if and wherever such third-party 
 *    acknowledgments normally appear.
 *
 * 4. The names "jPOS" and "jPOS.org" must not be used to endorse 
 *    or promote products derived from this software without prior 
 *    written permission. For written permission, please contact 
 *    license@jpos.org.
 *
 * 5. Products derived from this software may not be called "jPOS",
 *    nor may "jPOS" appear in their name, without prior written
 *    permission of the jPOS project.
 *
 * THIS SOFTWARE IS PROVIDED ``AS IS'' AND ANY EXPRESSED OR IMPLIED
 * WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.  
 * IN NO EVENT SHALL THE JPOS PROJECT OR ITS CONTRIBUTORS BE LIABLE FOR 
 * ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL 
 * DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS 
 * OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) 
 * HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, 
 * STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING 
 * IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE 
 * POSSIBILITY OF SUCH DAMAGE.
 * ====================================================================
 *
 * This software consists of voluntary contributions made by many
 * individuals on behalf of the jPOS Project.  For more
 * information please see <http://www.jpos.org/>.
 */

using System;
using System.Collections;
using System.Collections.Specialized;
using System.IO;
using System.Xml;
using System.Text;

namespace OLS.Msg
{
    public class FSDMsg
    {
            public static char FS = '\x1C';
            
            OrderedDictionary fields;
            String baseSchema;
            String basePath;
                    
             public FSDMsg(String basePath)
             {
                 fields = new OrderedDictionary();
                 this.basePath = basePath;
                 this.baseSchema = "base";
             } 
            public FSDMsg(String basePath, String baseSchema)
            {
                fields = new OrderedDictionary();
                this.basePath = basePath;
                this.baseSchema = baseSchema;
            }
            public String getBasePath()
            {
                return basePath;
            }
            public String getBaseSchema()
            {
                return baseSchema;
            }
            public String pack () 
            {
                StringBuilder sb = new StringBuilder ();
                pack (getSchema (baseSchema), sb);
                return sb.ToString();
            }
            protected String get (String id, String type, int length, String defValue) 
            {
                String value = (String) fields[id];
                if (value == null)
                    value = defValue == null ? "" : defValue;

                type   = type.ToUpper();
                    
                switch (Char.Parse(type.Substring(0,1))) {
                    case 'N':
                        value = value.PadLeft(length,'\x30');
                        break;
                    case 'A':
                        value = value.PadRight(length, '\x20');
                        break;
                    case 'K':
                        if (defValue != null)
                            value = defValue;
                        break;
                    case 'B':
                        break;
                }
                return (type.EndsWith ("FS")) ? value.Trim() : value;
            }
            public void unpack (byte[] b) 
            {
                unpack(new MemoryStream(b));   
            }
            public void unpack (Stream inputStream) {
                try {
                    unpack (inputStream, getSchema (baseSchema));
                } catch (EndOfStreamException) {
                    fields.Add ("EOF", "true");
                }
            }
            protected void unpack (Stream inputStream, XmlElement schema) 
            {
                IEnumerator iter = schema.ChildNodes.GetEnumerator();
                String keyOff = "";
                while (iter.MoveNext())
                {
                    XmlElement elem = (XmlElement)iter.Current;       
                    String id = elem.GetAttribute("id");
                    int length = Int32.Parse(elem.GetAttribute("length"));
                    String type = elem.GetAttribute("type").ToUpper();
                    bool key = "true".Equals(elem.GetAttribute("key"));
                    String value = readField (inputStream, id, length, type.EndsWith  ("FS"), "B".Equals (type));
                    if (key)
                        keyOff = keyOff + value;
                    if (keyOff.Length > 0) {
                     unpack (inputStream, getSchema (getId (schema) + keyOff));
                    }
                }
            }
           protected String read (Stream inputStream, int len, bool fs) 
            {
                StringBuilder sb = new StringBuilder();
                byte[] b = new byte[1];
                for (int i=0; i<len; i++) {
                    if (inputStream.Read(b,0,1) < 0)
                        throw new EndOfStreamException ();
                    if (fs && b[0] == FS) {
                        fs = false;
                        break;
                    }
                    sb.Append ((char) (b[0] & 0xff));
                }
                if (fs) {
                    if (inputStream.Read(b,0,1) < 0)
                        throw new EndOfStreamException ();
                }
                return sb.ToString ();
            }
            protected String readField (Stream inputStream, String fieldName, int len, bool fs, bool binary) 
            {
                String fieldValue = read (inputStream, len, fs);
                fields.Add (fieldName, fieldValue);
                return fieldValue;
            }
            private String getId(XmlElement e)
            {
                String s = e.GetAttribute("id");
                return s == null ? "" : s;
            }
            protected void pack (XmlElement schema, StringBuilder sb) 
            {   
                String keyOff = "";
                IEnumerator iter = schema.ChildNodes.GetEnumerator();
                while (iter.MoveNext())
                {
                    XmlElement elem = (XmlElement)iter.Current;
                    String id   = elem.GetAttribute("id");
                    int length = Int32.Parse(elem.GetAttribute("length"));
                    String type = elem.GetAttribute("type");
                    bool key = "true".Equals(elem.GetAttribute("key"));
                    String defValue = elem.InnerText;
                    String value = get(id, type, length, defValue);
                    sb.Append (value);
                    if (type.EndsWith ("FS"))
                        sb.Append (FS);
                    if (key) 
                        keyOff = keyOff + value;
                }
                if (keyOff.Length > 0) 
                    pack (getSchema (getId (schema) + keyOff), sb);     
            }
            public String get(String name)
            {
                return (String)fields[name];
            }
            public void set(String name, String value)
            {
                if (value != null)
                    fields.Add(name, value);
                else
                    fields.Remove(name);
            }        
            protected XmlElement getSchema (String message) 
            {
                StringBuilder sb = new StringBuilder (basePath);
                sb.Append (message);
                sb.Append (".xml");
                String uri = sb.ToString ();
                XmlDocument xmldoc = new XmlDocument();
                XmlElement schema; 
                xmldoc.Load(uri);
                XmlNodeList elemList = xmldoc.GetElementsByTagName("schema");
                schema = (XmlElement)elemList[0];                
                return schema;
            }
            public void dump(TextWriter p, String indent)
            {
                String inner = indent + "  ";
                p.WriteLine(indent + "<fsdmsg schema='" + basePath + baseSchema + "'>");
                IDictionaryEnumerator iter = fields.GetEnumerator();
                while (iter.MoveNext())
                {
                    String f = (String)iter.Key;
                    String v = ((String)iter.Value);
                    append(p, f, v, inner);
                }
                p.WriteLine(indent + "</fsdmsg>");
            }
            private void append(TextWriter p, String f, String v, String indent)
            {
                p.WriteLine(indent + f + ": '" + v + "'");
            }

        }
}