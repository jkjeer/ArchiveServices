<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:ms="urn:schemas-microsoft-com:xslt">

	<xsl:template match="root">
		<html>
			<head>
				<style>
					body,td,p,th  { font-family: arial; font-size: 8pt; }
					tr.head { border : 0px solid black; padding: 0 0px; background: #ddddff; font-weight:bold;}
					td.head { border : 0px solid black; padding: 0 0px; background: #ddddff; }
					td.numeric { text-align:right; width:80px;  }
					td.noBorder {border: none; }
					.error {color: red;}
					.title {font-weight:bold; text-decoration: underline;}
					.depth1 {text-indent: 0px;}
					.depth2 {text-indent: 5px;}
					.depth3 {text-indent: 10px;}
					.depth4 {text-indent: 15px;}
					.depth5 {text-indent: 20px;}
					.depth6 {text-indent: 25px;}
					.depth7 {text-indent: 30px;}
					.depth8 {text-indent: 35px;}
					.depth9 {text-indent: 40px;}
					.depth10 {text-indent: 45px;}
					.depth11 {text-indent: 50px;}
					.depth12 {text-indent: 55px;}
					.depth13 {text-indent: 60px;}
					.depth14 {text-indent: 65px;}
					.depth15 {text-indent: 70px;}
					tr.odd { background: #ffffff; }
					tr.even { background: #D5CFCF; }
					tr.stepodd { background: #ffffcc; }
					tr.stepeven { background: #e6ffe6; }
					.defaultTable td
					{
					border: 1px solid black;
					border-collapse:collapse;
					padding:2px;
					}
				</style>
			</head>
			<body>
				<div class="title">Tail Table</div>
				<br/>
				<table class="defaultTable">
					<tr>
						<td>ConfigID</td>
						<td>
							<xsl:value-of select="//TailTableConfig/ID"/>
						</td>
					</tr>
					<tr>
						<td>Table</td>
						<td>
							<xsl:value-of select="//TailTableConfig/ServerQualifiedName"/>
						</td>
					</tr>
					<tr>
						<td>Key Field</td>
						<td>
							<xsl:value-of select="//TailTableConfig/KeyColumn"/>
						</td>
					</tr>
					<tr>
						<td>Date Field</td>
						<td>
							<xsl:value-of select="//TailTableConfig/DateColumn"/>
						</td>
					</tr>
					<tr>
						<td>Retention Days</td>
						<td>
							<xsl:value-of select="//TailTableConfig/RetentionDays"/>
						</td>
					</tr>
					<tr>
						<td>Max Delete Rows Per Batch</td>
						<td>
							<xsl:value-of select="format-number(//TailTableConfig/MaxDeleteRowsPerBatch, '#,##0')"/>
						</td>
					</tr>
				</table>

				<br/>
				<table class="defaultTable">
					<tr class="head">
						<td align="left">Start Time</td>
						<td align="left">End Time</td>
						<td align="left">Elapsed Seconds</td>
						<td align="left">Initial Rows</td>
						<td align="left">Rows To Delete</td>
						<td align="left">Rows Deleted</td>
						<td align="left">Final Rows</td>
						<td align="left">Min SF Date</td>
						<td align="left">Archive Date</td>
						<td align="left">SF Days</td>
						<td align="left">Batch ID</td>
						<td align="left">Process ID</td>
					</tr>
					<xsl:apply-templates select="SummaryHeader/SummaryHeader">
					</xsl:apply-templates>
				</table>

				<xsl:if test="//Error">
					<br/>
					<div class="title">Errors</div>
					<br/>
					<table class="defaultTable">
						<tr class="head">
							<td align="left">Message</td>
							<td align="left">Source</td>
							<td align="left">Stack Trace</td>
						</tr>
						<xsl:apply-templates select="//Error">
						</xsl:apply-templates>
					</table>
					<br/>
				</xsl:if>

				<xsl:if test="ProcessResponse/Messages/Message">
					<br/>
					<div class="title">Messages</div>
					<br/>
					<table class="defaultTable">
						<tr class="head">
							<td align="left">Name</td>
							<td align="left">Value</td>
						</tr>
						<xsl:apply-templates select="ProcessResponse/Messages/Message"/>
					</table>
					<br/>
				</xsl:if>

				<xsl:if test="ProcessResponse">
					<br/>
					<div class="title">Process Steps</div>
					<br/>
					<table class="defaultTable">
						<tr class="head">
							<td align="left">Name</td>
							<td align="left">Start Date</td>
							<td align="left">End Date</td>
							<td align="left">Elapsed(Sec)</td>
							<td align="left">Rows Affected</td>
							<td align="left">Messages</td>
						</tr>
						<xsl:apply-templates select="ProcessResponse/ProcessSteps/ProcessStep">
							<xsl:sort select="StartDate"/>
						</xsl:apply-templates>
					</table>
					<br/>
				</xsl:if>

				<!--
        <div class="title">Select Command</div>
        <br/>
        <table class="defaultTable">
          <tr class="head">
            <td align="left">Statement</td>
          </tr>
          <tr>
            <td>
              <xsl:value-of select="//SelectCommand"/>
            </td>
          </tr>
        </table>
        <br/>

		  <div class="title">Delete Command</div>
		  <br/>
		  <table class="defaultTable">
			  <tr class="head">
				  <td align="left">Statement</td>
			  </tr>
			  <tr>
				  <td>
					  <xsl:value-of select="//DeleteCommand"/>
				  </td>
			  </tr>
		  </table>
		  <br/>
-->

				<xsl:if test="ProcessResponse/ConnectionStrings">
					<div class="title">Connection Strings</div>
					<br/>
					<table class="defaultTable">
						<tr class="head">
							<td align="left">Name</td>
							<td align="left">Value</td>
						</tr>
						<xsl:apply-templates select="ProcessResponse/ConnectionStrings/ConnectionString">
							<xsl:sort select="Name"/>
						</xsl:apply-templates>
					</table>
					<br/>
				</xsl:if>

				<br/>
				<table class="defaultTable">
					<tr>
						<td>Assembly Version</td>
						<td>
							<xsl:value-of select="//AssemblyVersion"/>
						</td>
					</tr>
					<tr>
						<td>User Name</td>
						<td>
							<xsl:value-of select="//ServiceContext/UserName"/>
						</td>
					</tr>
					<tr>
						<td>Session Id</td>
						<td>
							<xsl:value-of select="//ServiceContext/SessionId"/>
						</td>
					</tr>
					<tr>
						<td>Server Address</td>
						<td>
							<xsl:value-of select="//ServiceContext/ServerAddress"/>
						</td>
					</tr>
				</table>

			</body>
		</html>
	</xsl:template>

	<xsl:template match="SummaryHeader/SummaryHeader">
		<tr>
			<td>
				<xsl:value-of select="StartDateString"/>
			</td>
			<td>
				<xsl:value-of select="EndDateString"/>
			</td>
			<td align="right">
				<xsl:value-of select="format-number(ElapsedSeconds, '#,##0.00')"/>
			</td>
			<td align="right">
				<xsl:value-of select="format-number(InitialRowCount, '#,##0')"/>
			</td>
			<td align="right">
				<xsl:value-of select="format-number(RowsToDelete, '#,##0')"/>
			</td>
			<td align="right">
				<xsl:value-of select="format-number(RowsAffected, '#,##0')"/>
			</td>
			<td align="right">
				<xsl:value-of select="format-number(FinalRowCount, '#,##0')"/>
			</td>
			<td>
				<xsl:value-of select="SFMinDateString"/>
			</td>
			<td>
				<xsl:value-of select="ArchiveDateString"/>
			</td>
			<td align="right">
				<xsl:value-of select="format-number(SFDays, '#,##0')"/>
			</td>
			<td align="right">
				<xsl:value-of select="BatchID"/>
			</td>
			<td align="right">
				<xsl:value-of select="ProcessID"/>
			</td>
			<!--
      <td>
        <xsl:value-of select="TransferStatus"/>
      </td>
      <td>
        <xsl:value-of select="ms:format-date(TransferDate, 'MM/dd/yyyy','en-US')"/>&#160;
        <xsl:value-of select="ms:format-time(substring(Transfer/TransferDate, 1, 23 ), 'hh:mm:sstt', 'en-US')"/>
      </td>
      <td>
        <xsl:value-of select="HasErrors"/>
      </td>
      <td>
        <xsl:value-of select="TransferMessage"/>
      </td>
-->
		</tr>
	</xsl:template>

	<xsl:template match="ProcessResponse">
		<tr>
			<xsl:attribute name="class">
				<xsl:choose>
					<xsl:when test="position() mod 2 = 0">even</xsl:when>
					<xsl:otherwise>odd</xsl:otherwise>
				</xsl:choose>
			</xsl:attribute>
			<td class="depth{count(ancestor::*)}">
				<xsl:value-of select="ProcessName"/>
			</td>
			<td>
				<xsl:value-of select="StartDate"/>
			</td>
			<td>
				<xsl:value-of select="EndDate"/>
			</td>
			<td align="right">
				<xsl:value-of select="format-number(ElapsedSeconds, '#,##0.00')"/>
			</td>
			<td align="right">
				<xsl:value-of select="format-number(RowsAffected, '#,##0')"/>
			</td>
			<td>
				<xsl:value-of select="Errors/Error/Message"/>
			</td>
		</tr>
		<xsl:if test="ProcessSteps/ProcessStep">
			<xsl:apply-templates select="ProcessSteps/ProcessStep">
				<xsl:sort select="StartDate"/>
			</xsl:apply-templates>
		</xsl:if>
	</xsl:template>

	<xsl:template match="ProcessStep">
		<tr>
			<xsl:attribute name="class">
				<xsl:choose>
					<xsl:when test="position() mod 2 = 0">stepeven</xsl:when>
					<xsl:otherwise>stepeven</xsl:otherwise>
				</xsl:choose>
			</xsl:attribute>
			<td class="depth{count(ancestor::*)}">
				<xsl:value-of select="StepName"/>
			</td>
			<td>
				<xsl:value-of select="StartDate"/>
			</td>
			<td>
				<xsl:value-of select="EndDate"/>
			</td>
			<td align="right">
				<xsl:value-of select="format-number(ElapsedSeconds, '#,##0.00')"/>
			</td>
			<td align="right">
				<xsl:value-of select="format-number(RowsAffected, '#,##0')"/>
			</td>
			<td>
				<xsl:value-of select="Errors/Error/Message"/>
			</td>
		</tr>
		<xsl:if test="ProcessResponses">
			<xsl:apply-templates select="ProcessResponse/ProcessSteps/ProcessStep">
				<xsl:sort select="StartDate"/>
			</xsl:apply-templates>
		</xsl:if>
	</xsl:template>

	<xsl:template match="ConnectionString">
		<tr>
			<xsl:attribute name="class">
				<xsl:choose>
					<xsl:when test="position() mod 2 = 0">even</xsl:when>
					<xsl:otherwise>odd</xsl:otherwise>
				</xsl:choose>
			</xsl:attribute>
			<td>
				<xsl:value-of select="Name"/>
			</td>
			<td>
				<xsl:value-of select="Value"/>
			</td>
		</tr>
	</xsl:template>

	<xsl:template match="//Error">
		<tr>
			<xsl:attribute name="class">
				<xsl:choose>
					<xsl:when test="position() mod 2 = 0">even</xsl:when>
					<xsl:otherwise>odd</xsl:otherwise>
				</xsl:choose>
			</xsl:attribute>
			<td>
				<xsl:value-of select="Message"/>
			</td>
			<td>
				<xsl:value-of select="Source"/>
			</td>
			<td>
				<xsl:value-of select="StackTrace"/>
			</td>
		</tr>
	</xsl:template>

	<xsl:template match="Message">
		<tr>
			<xsl:attribute name="class">
				<xsl:choose>
					<xsl:when test="position() mod 2 = 0">even</xsl:when>
					<xsl:otherwise>odd</xsl:otherwise>
				</xsl:choose>
			</xsl:attribute>
			<td>
				<xsl:value-of select="Name"/>
			</td>
			<td>
				<xsl:value-of select="Value"/>
			</td>
		</tr>
	</xsl:template>

</xsl:stylesheet>

