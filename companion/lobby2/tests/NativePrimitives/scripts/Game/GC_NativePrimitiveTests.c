//! Isolated parser contract checks; no moderation actions or live state changes.
class GC_NativePrimitiveTests
{
	protected int m_iChecks;
	protected int m_iFailures;

	protected void Date(string input, bool expected, int epoch = 0)
	{
		int actual = -42;
		bool accepted = GC_AdminBanExpiry.ParseDate(input, actual);
		m_iChecks++;
		if (accepted == expected && (!accepted || actual == epoch))
			return;
		m_iFailures++;
		Print("GC | Native date FAILED | " + input, LogLevel.ERROR);
	}

	string Run()
	{
		Date("1970-01-01 00:00", true, 0);
		Date("1970-01-01 00:01", true, 60);
		Date("1972-02-29 12:34", true, 68214840);
		Date("2000-02-29 23:59", true, 951868740);
		Date("2024-02-29 00:00", true, 1709164800);
		Date("2037-12-31 23:59", true, 2145916740);
		Date("1970-01-28 01:02", true, 2336520);
		Date("1970-02-28 01:02", true, 5014920);
		Date("1970-03-28 01:02", true, 7434120);
		Date("1970-04-28 01:02", true, 10112520);
		Date("1970-05-28 01:02", true, 12704520);
		Date("1970-06-28 01:02", true, 15382920);
		Date("1970-07-28 01:02", true, 17974920);
		Date("1970-08-28 01:02", true, 20653320);
		Date("1970-09-28 01:02", true, 23331720);
		Date("1970-10-28 01:02", true, 25923720);
		Date("1970-11-28 01:02", true, 28602120);
		Date("1970-12-28 01:02", true, 31194120);
		Date("2000-01-28 01:02", true, 949021320);
		Date("2000-02-28 01:02", true, 951699720);
		Date("2000-03-28 01:02", true, 954205320);
		Date("2000-04-28 01:02", true, 956883720);
		Date("2000-05-28 01:02", true, 959475720);
		Date("2000-06-28 01:02", true, 962154120);
		Date("2000-07-28 01:02", true, 964746120);
		Date("2000-08-28 01:02", true, 967424520);
		Date("2000-09-28 01:02", true, 970102920);
		Date("2000-10-28 01:02", true, 972694920);
		Date("2000-11-28 01:02", true, 975373320);
		Date("2000-12-28 01:02", true, 977965320);
		Date("2023-01-28 01:02", true, 1674867720);
		Date("2023-02-28 01:02", true, 1677546120);
		Date("2023-03-28 01:02", true, 1679965320);
		Date("2023-04-28 01:02", true, 1682643720);
		Date("2023-05-28 01:02", true, 1685235720);
		Date("2023-06-28 01:02", true, 1687914120);
		Date("2023-07-28 01:02", true, 1690506120);
		Date("2023-08-28 01:02", true, 1693184520);
		Date("2023-09-28 01:02", true, 1695862920);
		Date("2023-10-28 01:02", true, 1698454920);
		Date("2023-11-28 01:02", true, 1701133320);
		Date("2023-12-28 01:02", true, 1703725320);
		Date("2037-01-28 01:02", true, 2116717320);
		Date("2037-02-28 01:02", true, 2119395720);
		Date("2037-03-28 01:02", true, 2121814920);
		Date("2037-04-28 01:02", true, 2124493320);
		Date("2037-05-28 01:02", true, 2127085320);
		Date("2037-06-28 01:02", true, 2129763720);
		Date("2037-07-28 01:02", true, 2132355720);
		Date("2037-08-28 01:02", true, 2135034120);
		Date("2037-09-28 01:02", true, 2137712520);
		Date("2037-10-28 01:02", true, 2140304520);
		Date("2037-11-28 01:02", true, 2142982920);
		Date("2037-12-28 01:02", true, 2145574920);
		Date("", false);
		Date("1969-12-31 23:59", false);
		Date("2038-01-01 00:00", false);
		Date("2023-02-29 00:00", false);
		Date("2000-02-30 00:00", false);
		Date("2024-04-31 00:00", false);
		Date("2024-00-01 00:00", false);
		Date("2024-13-01 00:00", false);
		Date("2024-01-00 00:00", false);
		Date("2024-01-32 00:00", false);
		Date("2024-01-01 24:00", false);
		Date("2024-01-01 00:60", false);
		Date("2024-01-01 00:-1", false);
		Date("2024-01-01T00:00", false);
		Date("2024/01/01 00:00", false);
		Date("2024-1-01 00:00", false);
		Date("2024-01-01 00:00Z", false);
		Date("abcd-01-01 00:00", false);
		Date("2024-0a-01 00:00", false);
		Date("2024-01-0a 00:00", false);
		Date("2024-01-01 0a:00", false);
		Date("2024-01-01 00:0a", false);
		array<string> numbers = {"0", "123456789", "0001", "", "1234567890", "-1", "+1", "1.0", " 1", "1 ", "1a"};
		foreach (int index, string number : numbers)
		{
			int value;
			m_iChecks++;
			if (GC_AdminBanExpiry.ParseNumber(number, value) != (index < 3))
				m_iFailures++;
		}
		return string.Format("checks=%1 failures=%2", m_iChecks, m_iFailures);
	}
}

modded class GC_LobbyManager
{
	string GC_TestNativePrimitives()
	{
		GC_NativePrimitiveTests checks = new GC_NativePrimitiveTests();
		return checks.Run();
	}
}
