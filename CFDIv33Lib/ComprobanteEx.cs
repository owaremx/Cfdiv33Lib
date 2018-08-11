using Chilkat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml.Serialization;
using System.Xml.Xsl;

namespace CFDIv33Lib
{
    public partial class Comprobante
    {
        [System.Xml.Serialization.XmlIgnore]
        public String RutaXSLTCadenaOriginal { get; set; }
        [System.Xml.Serialization.XmlIgnore]
        public String RutaXSDCfdi { get; set; }
        [XmlIgnore]
        public List<ImpuestosLocalesTrasladosLocales> ImpuestosLocalesTraslados { get; set; }

        public Comprobante() {
            this.versionField = "3.3";
            Conceptos = new List<ComprobanteConcepto>();
            ImpuestosLocalesTraslados = new List<ImpuestosLocalesTrasladosLocales>();
        }

        public Comprobante(String RutaXSLTCadenaOriginal, String RutaXSDCfdi)
        {
            ImpuestosLocalesTraslados = new List<ImpuestosLocalesTrasladosLocales>();
            //procesamiento de impuestos
            this.versionField = "3.3";
            Conceptos = new List<ComprobanteConcepto>();
            this.RutaXSLTCadenaOriginal = RutaXSLTCadenaOriginal;
            this.RutaXSDCfdi = RutaXSDCfdi;
        }

        public CfdiResult AgregarCertificado(String rutaCert)
        {
            Cert cert = new Cert() ;
            if (!cert.LoadFromFile(rutaCert))
                return new CfdiResult() { Mensaje = cert.LastErrorText, Correcto = false };

            if(cert.Expired)
                return new CfdiResult() { Mensaje = "Certificado expirado", Correcto = false };

            String serie = cert.SerialNumber;

            String noCertificado = "";
            for (int i = 1; i < serie.Length; i += 2)
                noCertificado += serie[i];

            this.Certificado = cert.GetEncoded().Replace(Environment.NewLine, "");
            this.NoCertificado = noCertificado;

            return new CfdiResult();
        }

        private String GetCadenaOriginal(String rutaXsltCadenaOriginal, String rutaXml)
        {
            //String rutaComprobanteTemp = Path.GetTempFileName();
            String rutaSalidaTemp = Path.GetTempFileName();
            //hguardar el xml del comprobante sin sellar para poder obtener la cadena original
            //EscribirXml(rutaXml, false, "", "", "", "");


            XslCompiledTransform xsl = new XslCompiledTransform();
            xsl.Load(rutaXsltCadenaOriginal);
            xsl.Transform(rutaXml, rutaSalidaTemp);
            xsl = null;

            StreamReader reader = new StreamReader(rutaSalidaTemp);
            String cadena = reader.ReadToEnd();
            reader.Close();
            reader.Dispose();
            reader = null;

            //eliminar archivos temporales
            //File.Delete(rutaComprobanteTemp);
            File.Delete(rutaSalidaTemp);

            return cadena;

            //String rutaSalidaTemp = Path.GetTempFileName();
            //String rutaEntradaTemp = Path.GetTempFileName();

            //XElement xDoc = XDocument.Load(rutaXml).Root;
            //xDoc.Save(rutaEntradaTemp);

            //XslCompiledTransform xsl = new XslCompiledTransform();
            //xsl.Load(rutaXsltCadenaOriginal);

            //xsl.Transform(rutaXml, rutaSalidaTemp);
            //xsl = null;

            //StreamReader reader = new StreamReader(rutaSalidaTemp);
            //String cadena = reader.ReadToEnd();
            //reader.Close();
            //reader.Dispose();
            //reader = null;

            ////eliminar archivos temporales
            //File.Delete(rutaSalidaTemp);
            //File.Delete(rutaEntradaTemp);

            //return cadena;
        }

        public CfdiResult SellarXml(String rutaCert, String rutaKey, String contrasena, String rutaArchivoXmlOrigen)
        {
            CfdiResult result = AgregarCertificado(rutaCert);
            if (!result.Correcto)
                return result;

            PrivateKey privKey = new PrivateKey();

            XDocument xDoc = XDocument.Load(rutaArchivoXmlOrigen);
            xDoc.Root.SetAttributeValue("Certificado", this.Certificado);
            xDoc.Root.SetAttributeValue("NoCertificado", this.NoCertificado);
            xDoc.Save(rutaArchivoXmlOrigen);

            String cadenaOriginal = GetCadenaOriginal(this.RutaXSLTCadenaOriginal, rutaArchivoXmlOrigen);

            if(!privKey.LoadPkcs8EncryptedFile(rutaKey, contrasena))
            {
                return new CfdiResult(false, privKey.LastErrorText);
            }

            String privKeyXml = privKey.GetXml();
            Rsa rsa = new Rsa();
            String hashAlg = "SHA-256";

            if (!rsa.UnlockComponent("RSAT34MB34N_7F1CD986683M"))
            {
                return new CfdiResult(false, rsa.LastErrorText);
            }

            if (!rsa.ImportPrivateKey(privKeyXml))
            {
                return new CfdiResult(false, rsa.LastErrorText);
            }

            rsa.Charset = "UTF-8";
            rsa.EncodingMode = "base64";
            rsa.LittleEndian = false;

            Cert cert = new Cert();
            if(!cert.LoadFromFile(rutaCert))
                return new CfdiResult(false, cert.LastErrorText);

            String selloBase64 = rsa.SignStringENC(cadenaOriginal, hashAlg);

            PublicKey publicKey = cert.ExportPublicKey();

            Rsa rsa2 = new Rsa();
            if (!rsa2.ImportPublicKey(publicKey.GetXml()))
            {
                return new CfdiResult(false, rsa2.LastErrorText);
            }
            rsa2.Charset = "utf-8";
            rsa2.EncodingMode = "base64";
            rsa2.LittleEndian = false;

            if(!rsa2.VerifyStringENC(cadenaOriginal, hashAlg, selloBase64))
            {
                return new CfdiResult(false, rsa2.LastErrorText);
            }

            cert.Dispose();
            privKey.Dispose();
            publicKey.Dispose();

            this.Sello = selloBase64;


            xDoc.Root.SetAttributeValue("Sello", selloBase64);

            //String xml = xDoc.ToString();
            //File.WriteAllText(rutaArchivoXmlOrigen, xml);
            xDoc.Save(rutaArchivoXmlOrigen);

            //SerializarObjeto(rutaArchivoXmlOrigen);

            return new CfdiResult();
        }

        private void ValidarXML(String rutaXml)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.Schemas.Add("http://www.sat.gob.mx/cfd/3", RutaXSDCfdi);
            settings.Schemas.Add("http://www.sat.gob.mx/implocal", "http://www.sat.gob.mx/cfd/implocal/implocal.xsd");
            settings.ValidationType = ValidationType.Schema;
            settings.ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings;

            XmlReader reader = XmlReader.Create(rutaXml, settings);
            while (reader.Read()) ;

            reader.Close();
        }
        
        public CfdiResult GenerarXml(String rutaSalida, String rutaCert, String rutaKey, String contrasena)
        {
            return EscribirXml(rutaSalida, true, rutaCert, rutaKey, contrasena, RutaXSLTCadenaOriginal); 
        }

        //Agregar nodo de impuestos locales 
        private void ProcesarImpuestosLocales(String rutaArchivo)
        {
            if (ImpuestosLocalesTraslados.Count == 0)
                return;

            SerializarObjeto(rutaArchivo);

            XDocument xDoc = XDocument.Load(rutaArchivo);
            
            XNamespace cfdi = "http://www.sat.gob.mx/cfd/3";
            XElement xComplemento = xDoc.Root.Element(cfdi + "Complemento");
            if(xComplemento == null)
            {
                xComplemento = new XElement(cfdi + "Complemento");
                xDoc.Root.Add(xComplemento);
            }

            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("implocal", "http://www.sat.gob.mx/implocal");

            XmlSerializer localesSerializer = new XmlSerializer(typeof(ImpuestosLocales), "implocal");
            ImpuestosLocales il = new ImpuestosLocales();
            il.TrasladosLocales = ImpuestosLocalesTraslados.ToArray();

            il.TotaldeTraslados = 0;
            foreach (ImpuestosLocalesTrasladosLocales t in ImpuestosLocalesTraslados)
                il.TotaldeTraslados += t.Importe;

            StringWriter writer = new StringWriter();
            localesSerializer.Serialize(writer, il, ns);

            XElement xlocales = XElement.Parse(writer.ToString());
            xComplemento.Add(xlocales);

            String xml = xDoc.ToString();

            File.WriteAllText(rutaArchivo, xml);
        }

        private CfdiResult EscribirXml(String rutaSalida, bool sellar, String rutaCert, String rutaKey, String contrasena, String rutaXsltCadenaOriginal)
        {
            decimal subtotal = 0;
            decimal impuestosTrasladados = 0;
            decimal impuestosRetenidos = 0;
            foreach (ComprobanteConcepto concepto in conceptosField)
            {
                subtotal += concepto.Importe;
                foreach (ComprobanteConceptoImpuestosTraslado i in concepto.Impuestos.Traslados)
                {
                    impuestosTrasladados += i.Importe;
                }

                if (concepto.Impuestos.Retenciones != null)
                {
                    foreach (ComprobanteConceptoImpuestosRetencion i in concepto.Impuestos.Retenciones)
                    {
                        impuestosRetenidos += i.Importe;
                    }
                }
            }

            ComprobanteImpuestosTraslado cImpuestoIVA = new ComprobanteImpuestosTraslado();
            cImpuestoIVA.Importe = impuestosTrasladados;
            cImpuestoIVA.TasaOCuota = Conceptos[0].Impuestos.Traslados[0].TasaOCuota;
            cImpuestoIVA.Impuesto = Conceptos[0].Impuestos.Traslados[0].Impuesto;

            decimal impuestosLocales = 0;
            //Impuestos locales, ISH
            foreach (ImpuestosLocalesTrasladosLocales l in ImpuestosLocalesTraslados)
                impuestosLocales += l.Importe;

            this.SubTotal = subtotal;
            this.Total = subtotal + impuestosTrasladados - this.Descuento + impuestosLocales;

            this.Impuestos = new ComprobanteImpuestos()
            {
                TotalImpuestosRetenidos = impuestosRetenidos,
                TotalImpuestosRetenidosSpecified = true,
                TotalImpuestosTrasladados = impuestosTrasladados,
                TotalImpuestosTrasladadosSpecified = true,
                Traslados = new ComprobanteImpuestosTraslado[] { cImpuestoIVA }
            };

            if (sellar)
            {
                if (ImpuestosLocalesTraslados.Count > 0)
                {

                    ProcesarImpuestosLocales(rutaSalida);
                }
                else
                    SerializarObjeto(rutaSalida);

                CfdiResult r = SellarXml(rutaCert, rutaKey, contrasena, rutaSalida);
                if (!r.Correcto)
                    return r;
            }
            else
                SerializarObjeto(rutaSalida);

            if (sellar)
                ValidarXML(rutaSalida);

            return new CfdiResult();
        }
        
        private void SerializarObjeto(String rutaSalida)
        {
            XmlSerializerNamespaces xNs = new XmlSerializerNamespaces();
            xNs.Add("cfdi", "http://www.sat.gob.mx/cfd/3");
            xNs.Add("xsi", "http://www.w3.org/2001/XMLSchema-instance");
            xNs.Add("implocal", "http://www.sat.gob.mx/implocal");

            XmlSerializer serializer = new XmlSerializer(typeof(Comprobante));
            XmlTextWriter writer = new XmlTextWriter(rutaSalida, Encoding.UTF8);

            writer.Formatting = Formatting.Indented;
            writer.Indentation = 4;
            serializer.Serialize(writer, this, xNs);
            writer.Close();

        }
    }
}
