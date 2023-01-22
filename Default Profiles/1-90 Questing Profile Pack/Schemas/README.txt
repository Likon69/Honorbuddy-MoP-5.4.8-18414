README.txt by chinajade
  $Rev: 3159 $
  $Date: 2014-03-19 03:34:46 -0300 (qua, 19 mar 2014) $

======================================================================
WHAT IS THIS?
-------------

We use the RelaxNG 'compact form' (rnc) to maintain the schema
definitions for Honorbuddy profiles.  RelaxNG 'compact form' has the
ability to translate from and to many other schema formats with no
loss of information.

We select RelaxNG compact form (rnc) for many reasons
enumerated in Eric van der Vlist's book (see References).


======================================================================
GENERATING XSD SCHEMA FILE FROM RELAX NG SCHEMA
-----------------------------------------------

Trang is an open-source tool that converts between different schema
languages for XML.  It supports the following languages:
* RELAX NG (XML syntax)
* RELAX NG compact syntax
* XML 1.0 DTDs
* W3C XML Schema

Trang can also infer a schema from one or more example XML documents.
We use this to generate generate an initial schema definition in
RelaxNG compact form (rnc).

We also use Trang to translate from the rnc format into W3C XML Schema
(xsd), since xsd is more widely supported in editors at the moment due
to its age.

To convert this grammar to XSD:
* Grab Trang from http://www.thaiopensource.com/relaxng/trang.html
* Run the following command:
  java.exe -jar trang.jar HBSchema-QuestProfile.rnc HBSchema-QuestProfile.xsd
* Enjoy your new XSD schema.
  PLEASE DO NOT CONVERT FROM XSD TO RNC--much information will be lost
  since RelaxNG is slightly more expressive than XSD.
   >>> The RelaxNG schema is considered the 'authoritative' schema source <<<

To facilitate the "to XSD" conversion process, we've included a
"rnc2xsd.bat" file.  Just double click on it to make it do the
conversion.  For the batch file to succeed, you need to be running a
machine with M$oft's PowerShell, and have Java installed in the
standard location.
   
   
======================================================================
REFERENCES
----------
* Eric van der Vlist's RELAX NG book:
  (Online) http://books.xmlschemata.org/relaxng/page2.html
  (O'Reilly) http://shop.oreilly.com/product/9780596004217.do

* Trang
  (Tool) http://www.thaiopensource.com/relaxng/trang.html
  (Manual) http://www.thaiopensource.com/relaxng/trang-manual.html

* W3C XML Schema Part 2: Datatypes Second Edition
  http://www.w3.org/TR/xmlschema-2/  



======================================================================
EDITOR INTEGRATION
------------------

Emacs
-----

Vim
---


