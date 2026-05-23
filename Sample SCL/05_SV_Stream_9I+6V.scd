<?xml version="1.0" encoding="UTF-8"?>
<SCL xmlns="http://www.iec.ch/61850/2003/SCL"
     xmlns:sxy="http://www.iec.ch/61850/2003/SCLcoordinates"
     xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
     xsi:schemaLocation="http://www.iec.ch/61850/2003/SCL SCL.xsd http://www.iec.ch/61850/2003/SCLcoordinates">
    <Header id="project"/>
    <Communication>
        <SubNetwork name="SubNetName" desc="SubNetDesc" type="8-MMS">
            <ConnectedAP iedName="MU05_9I_6V_1" apName="LD0">
                <SMV ldInst="MU05" cbName="MSVCB05">
                    <Address>
                        <P type="VLAN-ID" xsi:type="tP_VLAN-ID">0</P>
                        <P type="VLAN-PRIORITY" xsi:type="tP_VLAN-PRIORITY">4</P>
                        <P type="MAC-Address" xsi:type="tP_MAC-Address">01-0C-CD-04-00-01</P>
                        <P type="APPID" xsi:type="tP_APPID">4000</P>
                    </Address>
                </SMV>
            </ConnectedAP>
            <ConnectedAP iedName="MU05_9I_6V_2" apName="LD0">
                <SMV ldInst="MU05" cbName="MSVCB05">
                    <Address>
                        <P type="VLAN-ID" xsi:type="tP_VLAN-ID">0</P>
                        <P type="VLAN-PRIORITY" xsi:type="tP_VLAN-PRIORITY">4</P>
                        <P type="MAC-Address" xsi:type="tP_MAC-Address">01-0C-CD-04-00-02</P>
                        <P type="APPID" xsi:type="tP_APPID">4000</P>
                    </Address>
                </SMV>
            </ConnectedAP>
            <ConnectedAP iedName="MU05_9I_6V_3" apName="LD0">
                <SMV ldInst="MU05" cbName="MSVCB05">
                    <Address>
                        <P type="VLAN-ID" xsi:type="tP_VLAN-ID">0</P>
                        <P type="VLAN-PRIORITY" xsi:type="tP_VLAN-PRIORITY">4</P>
                        <P type="MAC-Address" xsi:type="tP_MAC-Address">01-0C-CD-04-00-03</P>
                        <P type="APPID" xsi:type="tP_APPID">4000</P>
                    </Address>
                </SMV>
            </ConnectedAP>
        </SubNetwork>
    </Communication>
    <IED name="MU05_9I_6V_1" desc="MUdesc" type="MUtype" manufacturer="MUmanufacturer" configVersion="G">
        <AccessPoint name="LD0">
            <Server>
                <LDevice desc="Std conformance" inst="MU05">
                    <LN0 desc="Std conformance" lnClass="LLN0" inst="" lnType="MULLN0_ED1_A1_0">
                        <DataSet name="PhsMeas5">
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="1" doName="AmpSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="1" doName="AmpSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="2" doName="AmpSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="2" doName="AmpSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="3" doName="AmpSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="3" doName="AmpSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="4" doName="AmpSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="4" doName="AmpSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="5" doName="AmpSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="5" doName="AmpSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="6" doName="AmpSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="6" doName="AmpSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="7" doName="AmpSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="7" doName="AmpSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="8" doName="AmpSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="8" doName="AmpSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="9" doName="AmpSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="9" doName="AmpSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="1" doName="VolSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="1" doName="VolSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="2" doName="VolSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="2" doName="VolSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="3" doName="VolSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="3" doName="VolSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="4" doName="VolSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="4" doName="VolSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="5" doName="VolSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="5" doName="VolSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="6" doName="VolSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="6" doName="VolSv" daName="q" fc="MX" />
                        </DataSet>
                        <SampledValueControl multicast="true" name="MSVCB05" datSet="PhsMeas5" confRev="1" smvID="OMICRON_CMC_SV1" smpRate="4000" smpMod="SmpPerSec" nofASDU="1">
                            <SmvOpts refreshTime="false" sampleSynchronized="true" sampleRate="false" dataSet="false" security="false" synchSourceId="false"/>
                        </SampledValueControl>
                    </LN0>
                    <LN lnType="CTR-P" lnClass="TCTR" inst="1"/>
                    <LN lnType="CTR-P" lnClass="TCTR" inst="2"/>
                    <LN lnType="CTR-P" lnClass="TCTR" inst="3"/>
                    <LN lnType="CTR-P" lnClass="TCTR" inst="4"/>
                    <LN lnType="CTR-P" lnClass="TCTR" inst="5"/>
                    <LN lnType="CTR-P" lnClass="TCTR" inst="6"/>
                    <LN lnType="CTR-P" lnClass="TCTR" inst="7"/>
                    <LN lnType="CTR-P" lnClass="TCTR" inst="8"/>
                    <LN lnType="CTR-P" lnClass="TCTR" inst="9"/>
                    <LN lnType="VTR-P" lnClass="TVTR" inst="1"/>
                    <LN lnType="VTR-P" lnClass="TVTR" inst="2"/>
                    <LN lnType="VTR-P" lnClass="TVTR" inst="3"/>
                    <LN lnType="VTR-P" lnClass="TVTR" inst="4"/>
                    <LN lnType="VTR-P" lnClass="TVTR" inst="5"/>
                    <LN lnType="VTR-P" lnClass="TVTR" inst="6"/>
                </LDevice>
            </Server>
        </AccessPoint>
    </IED>
    <IED name="MU05_9I_6V_2" desc="MUdesc" type="MUtype" manufacturer="MUmanufacturer" configVersion="G">
        <AccessPoint name="LD0">
            <Server>
                <LDevice desc="Std conformance" inst="MU05">
                    <LN0 desc="Std conformance" lnClass="LLN0" inst="" lnType="MULLN0_ED1_A1_0">
                        <DataSet name="PhsMeas5">
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="1" doName="AmpSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="1" doName="AmpSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="2" doName="AmpSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="2" doName="AmpSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="3" doName="AmpSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="3" doName="AmpSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="4" doName="AmpSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="4" doName="AmpSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="5" doName="AmpSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="5" doName="AmpSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="6" doName="AmpSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="6" doName="AmpSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="7" doName="AmpSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="7" doName="AmpSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="8" doName="AmpSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="8" doName="AmpSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="9" doName="AmpSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="9" doName="AmpSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="1" doName="VolSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="1" doName="VolSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="2" doName="VolSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="2" doName="VolSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="3" doName="VolSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="3" doName="VolSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="4" doName="VolSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="4" doName="VolSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="5" doName="VolSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="5" doName="VolSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="6" doName="VolSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="6" doName="VolSv" daName="q" fc="MX" />
                        </DataSet>
                        <SampledValueControl multicast="true" name="MSVCB05" datSet="PhsMeas5" confRev="1" smvID="OMICRON_CMC_SV2" smpRate="4000" smpMod="SmpPerSec" nofASDU="1">
                            <SmvOpts refreshTime="false" sampleSynchronized="true" sampleRate="false" dataSet="false" security="false" synchSourceId="false"/>
                        </SampledValueControl>
                    </LN0>
                    <LN lnType="CTR-P" lnClass="TCTR" inst="1"/>
                    <LN lnType="CTR-P" lnClass="TCTR" inst="2"/>
                    <LN lnType="CTR-P" lnClass="TCTR" inst="3"/>
                    <LN lnType="CTR-P" lnClass="TCTR" inst="4"/>
                    <LN lnType="CTR-P" lnClass="TCTR" inst="5"/>
                    <LN lnType="CTR-P" lnClass="TCTR" inst="6"/>
                    <LN lnType="CTR-P" lnClass="TCTR" inst="7"/>
                    <LN lnType="CTR-P" lnClass="TCTR" inst="8"/>
                    <LN lnType="CTR-P" lnClass="TCTR" inst="9"/>
                    <LN lnType="VTR-P" lnClass="TVTR" inst="1"/>
                    <LN lnType="VTR-P" lnClass="TVTR" inst="2"/>
                    <LN lnType="VTR-P" lnClass="TVTR" inst="3"/>
                    <LN lnType="VTR-P" lnClass="TVTR" inst="4"/>
                    <LN lnType="VTR-P" lnClass="TVTR" inst="5"/>
                    <LN lnType="VTR-P" lnClass="TVTR" inst="6"/>
                </LDevice>
            </Server>
        </AccessPoint>
    </IED>
    <IED name="MU05_9I_6V_3" desc="MUdesc" type="MUtype" manufacturer="MUmanufacturer" configVersion="G">
        <AccessPoint name="LD0">
            <Server>
                <LDevice desc="Std conformance" inst="MU05">
                    <LN0 desc="Std conformance" lnClass="LLN0" inst="" lnType="MULLN0_ED1_A1_0">
                        <DataSet name="PhsMeas5">
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="1" doName="AmpSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="1" doName="AmpSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="2" doName="AmpSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="2" doName="AmpSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="3" doName="AmpSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="3" doName="AmpSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="4" doName="AmpSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="4" doName="AmpSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="5" doName="AmpSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="5" doName="AmpSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="6" doName="AmpSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="6" doName="AmpSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="7" doName="AmpSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="7" doName="AmpSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="8" doName="AmpSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="8" doName="AmpSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="9" doName="AmpSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TCTR" lnInst="9" doName="AmpSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="1" doName="VolSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="1" doName="VolSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="2" doName="VolSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="2" doName="VolSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="3" doName="VolSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="3" doName="VolSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="4" doName="VolSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="4" doName="VolSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="5" doName="VolSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="5" doName="VolSv" daName="q" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="6" doName="VolSv" daName="instMag.i" fc="MX" />
                            <FCDA ldInst="MU05" prefix="" lnClass="TVTR" lnInst="6" doName="VolSv" daName="q" fc="MX" />
                        </DataSet>
                        <SampledValueControl multicast="true" name="MSVCB05" datSet="PhsMeas5" confRev="1" smvID="OMICRON_CMC_SV3" smpRate="4000" smpMod="SmpPerSec" nofASDU="1">
                            <SmvOpts refreshTime="false" sampleSynchronized="true" sampleRate="false" dataSet="false" security="false" synchSourceId="false"/>
                        </SampledValueControl>
                    </LN0>
                    <LN lnType="CTR-P" lnClass="TCTR" inst="1"/>
                    <LN lnType="CTR-P" lnClass="TCTR" inst="2"/>
                    <LN lnType="CTR-P" lnClass="TCTR" inst="3"/>
                    <LN lnType="CTR-P" lnClass="TCTR" inst="4"/>
                    <LN lnType="CTR-P" lnClass="TCTR" inst="5"/>
                    <LN lnType="CTR-P" lnClass="TCTR" inst="6"/>
                    <LN lnType="CTR-P" lnClass="TCTR" inst="7"/>
                    <LN lnType="CTR-P" lnClass="TCTR" inst="8"/>
                    <LN lnType="CTR-P" lnClass="TCTR" inst="9"/>
                    <LN lnType="VTR-P" lnClass="TVTR" inst="1"/>
                    <LN lnType="VTR-P" lnClass="TVTR" inst="2"/>
                    <LN lnType="VTR-P" lnClass="TVTR" inst="3"/>
                    <LN lnType="VTR-P" lnClass="TVTR" inst="4"/>
                    <LN lnType="VTR-P" lnClass="TVTR" inst="5"/>
                    <LN lnType="VTR-P" lnClass="TVTR" inst="6"/>
                </LDevice>
            </Server>
        </AccessPoint>
    </IED>
    <DataTypeTemplates>
        <LNodeType id="MULLN0_ED1_A1_0" lnClass="LLN0"/>
        <LNodeType id="CTR-P" lnClass="TCTR">
            <DO name="AmpSv" type="AmpSAV"/>
        </LNodeType>
        <LNodeType id="CTR-N" lnClass="TCTR">
            <DO name="AmpSv" type="AmpSAV"/>
        </LNodeType>
        <LNodeType id="VTR-P" lnClass="TVTR">
            <DO name="VolSv" type="VolSAV"/>
        </LNodeType>
        <LNodeType id="VTR-N" lnClass="TVTR">
            <DO name="VolSv" type="VolSAV"/>
        </LNodeType>
        <DOType id="AmpSAV" cdc="SAV">
            <DA name="instMag" bType="Struct" type="AnalogueValue" fc="MX"/>
            <DA name="q" bType="Quality" fc="MX" qchg="true"/>
        </DOType>
        <DOType id="VolSAV" cdc="SAV">
            <DA name="instMag" bType="Struct" type="AnalogueValue" fc="MX"/>
            <DA name="q" bType="Quality" fc="MX" qchg="true"/>
        </DOType>
        <DAType id="AnalogueValue">
            <BDA name="i" bType="INT32"/>
        </DAType>
    </DataTypeTemplates>
</SCL>