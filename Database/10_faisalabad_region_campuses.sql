-- =============================================================================
-- THE ARQAM SCHOOLS: FAISALABAD REGION CAMPUSES (3 November 2026)
-- Run after 09. Safe to run twice: it records itself in dbo.schema_migrations.
--
-- Source: "Updated Schools list 2026" (Faisalabad region), sent by the owner.
-- Adds 93 campuses (Samanabad is already on the website and is not repeated), with
-- the cities, districts and regions they need. Only the campus name, address, area and the
-- school's own phone number(s) are used. Directors' names and their cell numbers are NOT
-- added, as the owner asked. Campuses without a school phone have no number.
-- Row 63 (same address as row 41 in the list) is added hidden until the owner confirms it.
-- Existing cities without a district get one (backed up first). Nothing is deleted; a campus that already exists (same city and
-- web address) is skipped. Each campus can be edited or hidden in the admin panel.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF EXISTS (SELECT 1 FROM dbo.schema_migrations WHERE id = N'10_faisalabad_region_campuses')
BEGIN
    PRINT N'10_faisalabad_region_campuses is already applied. Nothing to do.';
    SET NOEXEC ON;
END
GO
BEGIN TRANSACTION;

DECLARE @c TABLE (row_no INT, name NVARCHAR(150), slug NVARCHAR(120), city NVARCHAR(100), city_slug NVARCHAR(120),
                  district NVARCHAR(100), district_slug NVARCHAR(120), region NVARCHAR(100), area NVARCHAR(120), area_slug NVARCHAR(120),
                  address NVARCHAR(300), phone NVARCHAR(20), phone_alt NVARCHAR(20), published BIT);
INSERT INTO @c VALUES
  (2, N'Peoples Colony Campus', N'peoples-colony-campus', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'Peoples Colony', N'peoples-colony', N'144/C, Peoples Colony No. 1, Faisalabad', N'041-8544206', N'041-8719766', 1),
  (3, N'Gulberg Colony Campus', N'gulberg-colony-campus', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'Gulberg Colony', N'gulberg-colony', N'195-C, Gulberg Colony, Jattanwala Chowk, Faisalabad', N'041-2618182', NULL, 1),
  (4, N'Gulistan Colony Campus', N'gulistan-colony-campus', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'Gulistan Colony', N'gulistan-colony', N'4-B, behind Ayesha Masjid, Gulistan Colony No. 2, Millat Road, Faisalabad', N'041-8787687', NULL, 1),
  (5, N'Nishatabad Campus', N'nishatabad-campus', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'Nishatabad', N'nishatabad', N'Akbar Town, Street No. 7, Main Malikpur Road, Nishatabad, Faisalabad', N'041-8751896', NULL, 1),
  (6, N'Umer-e-Farooq Campus', N'umer-e-farooq-campus', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'Gulfishan Colony', N'gulfishan-colony', N'P-2, Block A, Bohar Chowk, Gulfishan Colony, Jhang Road, Faisalabad', N'0308-0345058', NULL, 1),
  (7, N'Kareem Town Campus', N'kareem-town-campus', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'Kareem Town', N'kareem-town', N'35-C, Main Road, Kareem Town, Faisalabad', N'041-8714360', NULL, 1),
  (8, N'Ali Campus, Liaqat Town', N'ali-campus-liaqat-town', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'Liaqat Town', N'liaqat-town', N'110-111/C, Liaqat Town, Opposite Drain Nala, Faisalabad', N'0300-7661880', NULL, 1),
  (9, N'Depot Wala Bazar Campus', N'depot-wala-bazar-campus', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'Depot Wala Bazar', N'depot-wala-bazar', N'Depot Wala Bazar, Faisalabad', NULL, NULL, 1),
  (10, N'Mananwala Campus', N'mananwala-campus', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'Mananwala', N'mananwala', N'Mohalla Mughal Pura, Sheikhupura Road, Chak No. 203 RB Mananwala, Faisalabad', NULL, NULL, 1),
  (11, N'Millat Town Campus', N'millat-town-campus', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'Millat Town', N'millat-town', N'24-Khayaban-e-Kareem, Millat Town, Faisalabad', N'041-8814499', NULL, 1),
  (12, N'Sehgal City Campus', N'sehgal-city-campus', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'Sehgal City', N'sehgal-city', N'182, Sehgal City, Samundri Road, Faisalabad', N'041-2418040', NULL, 1),
  (13, N'G.M. Abad Campus', N'g-m-abad-campus', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'G.M. Abad', N'g-m-abad', N'567-A, Qasmia Park, G.M. Abad, Faisalabad', N'041-2693254', NULL, 1),
  (14, N'Makkuana Campus', N'makkuana-campus', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'Makkuana', N'makkuana', N'Near Police Line, Makkuana, Jaranwala Road, Faisalabad', NULL, NULL, 1),
  (15, N'Chiniot Campus', N'chiniot-campus', N'Chiniot', N'chiniot', N'Chiniot', N'chiniot', N'Punjab', N'Z Block', N'z-block', N'11/16 Z Block, Chiniot', N'047-6334628', NULL, 1),
  (16, N'Fatima-tuz-Zahra Campus, Dijkot', N'fatima-tuz-zahra-campus-dijkot', N'Dijkot', N'dijkot', N'Faisalabad', N'faisalabad', N'Punjab', N'Jameel Market', N'jameel-market', N'Jameel Market, Near Madrassa Qasimia, Dijkot', NULL, NULL, 1),
  (17, N'Gojra Campus', N'gojra-campus', N'Gojra', N'gojra', N'Toba Tek Singh', N'toba-tek-singh', N'Punjab', N'Pansrah Road', N'pansrah-road', N'Near Jinnah College, Pansrah Road, Gojra', N'046-3514453', NULL, 1),
  (18, N'Jaranwala Campus', N'jaranwala-campus', N'Jaranwala', N'jaranwala', N'Faisalabad', N'faisalabad', N'Punjab', N'Faisalabad Road', N'faisalabad-road', N'Near Hassan Travels, Faisalabad Road, Jaranwala', N'041-4313423', NULL, 1),
  (19, N'Chak Jhumra Campus', N'chak-jhumra-campus', N'Chak Jhumra', N'chak-jhumra', N'Faisalabad', N'faisalabad', N'Punjab', N'Chamra Mandi', N'chamra-mandi', N'Near Chamra Mandi, Chak Jhumra, District Faisalabad', N'041-8761296', N'041-8527744', 1),
  (20, N'Kamalia Campus', N'kamalia-campus', N'Kamalia', N'kamalia', N'Toba Tek Singh', N'toba-tek-singh', N'Punjab', N'Zeeshan Colony', N'zeeshan-colony', N'Main Road, Zeeshan Colony, Kamalia', N'046-3411222', NULL, 1),
  (21, N'Abdullah Campus, Pansara', N'abdullah-campus-pansara', N'Pansara', N'pansara', N'Faisalabad', N'faisalabad', N'Punjab', N'Jhang Road', N'jhang-road', N'Jhang Road, Pansara', N'041-2574245', NULL, 1),
  (22, N'Saeed Colony Campus', N'saeed-colony-campus', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'Saeed Colony', N'saeed-colony', N'P-88, Ahmad Mujtaba Street, Near Raza Car Wash Center, Susan Road, Saeed Colony No. 2, Faisalabad', N'0332-6677478', NULL, 1),
  (23, N'Samundri Campus', N'samundri-campus', N'Samundri', N'samundri', N'Faisalabad', N'faisalabad', N'Punjab', N'WAPDA Chowk', N'wapda-chowk', N'WAPDA Chowk, Faisalabad Road, Samundri', NULL, NULL, 1),
  (24, N'Tandlianwala Campus', N'tandlianwala-campus', N'Tandlianwala', N'tandlianwala', N'Faisalabad', N'faisalabad', N'Punjab', N'Canal View Road', N'canal-view-road', N'Behind Girls College, Canal View Road, Tandlianwala', N'041-3441166', NULL, 1),
  (25, N'Adda Phaloor Campus', N'adda-phaloor-campus', N'Adda Phaloor', N'adda-phaloor', N'Toba Tek Singh', N'toba-tek-singh', N'Punjab', N'Adda Phaloor', N'adda-phaloor', N'Near Telephone Exchange, Adda Phaloor, District Toba Tek Singh', N'046-2541295', NULL, 1),
  (26, N'Mamu Kanjan Campus', N'mamu-kanjan-campus', N'Mamu Kanjan', N'mamu-kanjan', N'Faisalabad', N'faisalabad', N'Punjab', N'Rail Bazar', N'rail-bazar', N'Opposite Sabzi Mandi Bridge, Near Rail Bazar, Mamu Kanjan', N'041-3431915', NULL, 1),
  (27, N'Nadeem Shaheed Campus', N'nadeem-shaheed-campus', N'Samundri', N'samundri', N'Faisalabad', N'faisalabad', N'Punjab', N'Adda Kaniyan Bangla', N'adda-kaniyan-bangla', N'Chak No. 166 GB, Adda Kaniyan Bangla, Tehsil Samundri, District Faisalabad', N'041-3411166', N'0344-7867168', 1),
  (28, N'Khurrianwala Campus', N'khurrianwala-campus', N'Khurrianwala', N'khurrianwala', N'Faisalabad', N'faisalabad', N'Punjab', N'Main Bazar', N'main-bazar', N'Near Pani Wali Tanki, Main Bazar, Jhumra Road, Khurrianwala, District Faisalabad', NULL, NULL, 1),
  (29, N'Gulshan Colony Campus', N'gulshan-colony-campus', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'Gulshan Colony', N'gulshan-colony', N'P-249, Gulshan Colony, Opposite Sezeena Bakers, Meelad Road, Faisalabad', N'041-2430254', NULL, 1),
  (30, N'Razaabad Campus', N'razaabad-campus', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'Razaabad', N'razaabad', N'P-123, Street No. 4, Bazar No. 3, Razaabad, Faisalabad', N'041-2690854', NULL, 1),
  (31, N'Nigehban Pura Campus', N'nigehban-pura-campus', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'Nigehban Pura', N'nigehban-pura', N'Near Tower Road, Nigehban Pura, Near Dr. Asia Clinic, Faisalabad', N'041-2417880', NULL, 1),
  (32, N'Eden Campus', N'eden-campus', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'Madni Garden', N'madni-garden', N'Madni Garden, Opposite Eden Garden Gate No. 2, Khayaban Colony No. 3, Faisalabad', NULL, NULL, 1),
  (33, N'Adda Zafar Chowk Campus', N'adda-zafar-chowk-campus', N'Samundri', N'samundri', N'Faisalabad', N'faisalabad', N'Punjab', N'Adda Zafar Chowk', N'adda-zafar-chowk', N'Adda Zafar Chowk, Mamu Kanjan Road, Tehsil Samundri, District Faisalabad', NULL, NULL, 1),
  (34, N'Saleem Campus', N'saleem-campus', N'Samundri', N'samundri', N'Faisalabad', N'faisalabad', N'Punjab', N'Adda 45 GB', N'adda-45-gb', N'Adda 45 GB, Samundri-Gojra Road, Tehsil Samundri, District Faisalabad', NULL, NULL, 1),
  (35, N'Izzah Campus', N'izzah-campus', N'Shorkot Cantt', N'shorkot-cantt', N'Jhang', N'jhang', N'Punjab', N'327 GB', N'327-gb', N'Main Market, 327 GB, Shorkot Cantt', N'047-5501745', NULL, 1),
  (36, N'New Samundri Campus', N'new-samundri-campus', N'Samundri', N'samundri', N'Faisalabad', N'faisalabad', N'Punjab', N'40 Moor', N'40-moor', N'Gojra Road, 40 Moor, New Samundri, Tehsil Samundri, District Faisalabad', N'041-3402045', NULL, 1),
  (37, N'2 Chak Campus', N'2-chak-campus', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'Islamia Park', N'islamia-park', N'House No. 1402, Street No. 8/4, Islamia Park, Block C, Near Abu Bakar Masjid, Faisalabad', NULL, NULL, 1),
  (38, N'Usman Ali Campus', N'usman-ali-campus', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'Ghulam Muhammad Abad', N'ghulam-muhammad-abad', N'587-B, Civil Quarter, Ghulam Muhammad Abad, Faisalabad', NULL, NULL, 1),
  (39, N'Khalid Bin Waleed Campus, Murad Shah', N'khalid-bin-waleed-campus-murad-shah', N'Gojra', N'gojra', N'Toba Tek Singh', N'toba-tek-singh', N'Punjab', N'Adda Murad Shah', N'adda-murad-shah', N'Adda Murad Shah, 421 JB, 13 km Moochi Wala Road, Tehsil Gojra, District Toba Tek Singh', N'0300-8717421', NULL, 1),
  (40, N'Gulistan Colony Campus, Sahiwal', N'gulistan-colony-campus-sahiwal', N'Sahiwal', N'sahiwal', N'Sahiwal', N'sahiwal', N'Punjab', N'Gulistan Colony', N'gulistan-colony', N'House No. 212, Gulistan Colony, Sahiwal', NULL, NULL, 1),
  (41, N'Uzair Campus', N'uzair-campus', N'Sandhilianwali', N'sandhilianwali', N'Toba Tek Singh', N'toba-tek-singh', N'Punjab', N'Abdul Hakeem Road', N'abdul-hakeem-road', N'Bhatti House, Near Dr. Mustafa Clinic, Abdul Hakeem Road, Sandhilianwali', N'046-3522574', NULL, 1),
  (42, N'Ahmad Campus, Zia Town', N'ahmad-campus-zia-town', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'Zia Town', N'zia-town', N'B-7, Street No. 2, Near Gohar Textile, Zia Town, East Canal Road, Faisalabad', NULL, NULL, 1),
  (43, N'Al Rehmat Campus', N'al-rehmat-campus', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'Raza Town', N'raza-town', N'204 Chak, Raza Town, Canal Road, Behind Sitara House, Faisalabad', NULL, NULL, 1),
  (44, N'Hira Campus', N'hira-campus', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'Madina Town', N'madina-town', N'Officers Colony No. 2, Madina Town, Faisalabad', NULL, NULL, 1),
  (45, N'Fatima-tuz-Zahra Campus, Sahiwal', N'fatima-tuz-zahra-campus-sahiwal', N'Sahiwal', N'sahiwal', N'Sahiwal', N'sahiwal', N'Punjab', N'Fateh Sher Colony', N'fateh-sher-colony', N'20/A, Main Market, Fateh Sher Colony, Sahiwal', N'040-4228868', NULL, 1),
  (46, N'Dhandra Campus', N'dhandra-campus', N'Dhandra', N'dhandra', N'Faisalabad', N'faisalabad', N'Punjab', N'66 JB Dhandra', N'66-jb-dhandra', N'66 JB Dhandra, Near Daras Stop', NULL, NULL, 1),
  (47, N'Dawood Campus, Kassowal', N'dawood-campus-kassowal', N'Kassowal', N'kassowal', N'Sahiwal', N'sahiwal', N'Punjab', N'Zameer Colony', N'zameer-colony', N'Zameer Colony, Kassowal, Tehsil Chichawatni, District Sahiwal', NULL, NULL, 1),
  (48, N'Usman-e-Ghani Campus', N'usman-e-ghani-campus', N'Mian Channu', N'mian-channu', N'Khanewal', N'khanewal', N'Punjab', N'Jinnah Town', N'jinnah-town', N'Street No. 3-A, Darbar Road, Jinnah Town, Mian Channu', NULL, NULL, 1),
  (49, N'Taqwa Campus', N'taqwa-campus', N'Okara', N'okara', N'Okara', N'okara', N'Punjab', N'Govt. Colony', N'govt-colony', N'14-Y, Govt. Colony, Near Commerce College, Okara', N'044-2552611', N'044-2552612', 1),
  (50, N'Abu Bakar Campus', N'abu-bakar-campus', N'Chichawatni', N'chichawatni', N'Sahiwal', N'sahiwal', N'Punjab', N'Okanwala Bangla', N'okanwala-bangla', N'Okanwala Bangla, Burewala Road, Chichawatni', NULL, NULL, 1),
  (51, N'Muslim Campus, Depalpur', N'muslim-campus-depalpur', N'Depalpur', N'depalpur', N'Okara', N'okara', N'Punjab', N'Okara Road', N'okara-road', N'Opposite Govt. Colony, Okara Road, Depalpur', NULL, NULL, 1),
  (52, N'Ali Campus, Noal Plot', N'ali-campus-noal-plot', N'Okara', N'okara', N'Okara', N'okara', N'Punjab', N'Adda Noal Plot', N'adda-noal-plot', N'Adda Noal Plot, Tehsil and District Okara', NULL, NULL, 1),
  (53, N'Muhammad Campus, Noorpur Noranga', N'muhammad-campus-noorpur-noranga', N'Noorpur Noranga', N'noorpur-noranga', N'Bahawalpur', N'bahawalpur', N'Punjab', N'Commercial Plaza', N'commercial-plaza', N'Commercial Plaza, Noorpur Noranga, District Bahawalpur', NULL, NULL, 1),
  (54, N'Muhammad Campus, Hujra Shah Muqeem', N'muhammad-campus-hujra-shah-muqeem', N'Hujra Shah Muqeem', N'hujra-shah-muqeem', N'Okara', N'okara', N'Punjab', N'Kasur Road', N'kasur-road', N'Kasur Road, Chowk Hujra Shah Muqeem', N'044-4860573', NULL, 1),
  (55, N'Fatima-tuz-Zahra Campus, Head Rajkan', N'fatima-tuz-zahra-campus-head-rajkan', N'Head Rajkan', N'head-rajkan', N'Bahawalpur', N'bahawalpur', N'Punjab', N'Adda 13000', N'adda-13000', N'Adda 13000, Head Rajkan, Tehsil Yazman, District Bahawalpur', NULL, NULL, 1),
  (56, N'Al Razi Campus', N'al-razi-campus', N'Bahawalpur', N'bahawalpur', N'Bahawalpur', N'bahawalpur', N'Punjab', N'Yazman Road', N'yazman-road', N'Near Al Fareed Pump, Yazman Road, Bahawalpur', NULL, NULL, 1),
  (57, N'Khalid Bin Waleed Campus, Ahmedpur East', N'khalid-bin-waleed-campus-ahmedpur-east', N'Ahmedpur East', N'ahmedpur-east', N'Bahawalpur', N'bahawalpur', N'Punjab', N'Faiz Colony', N'faiz-colony', N'Al-Ghani Manzil, Faiz Colony, Near Ameer Alam Colony, Ahmedpur East, District Bahawalpur', N'062-2273188', NULL, 1),
  (58, N'Nisar-e-Mustafa Campus', N'nisar-e-mustafa-campus', N'Khanpur', N'khanpur', N'Rahim Yar Khan', N'rahim-yar-khan', N'Punjab', N'Jinnah Town', N'jinnah-town', N'Jinnah Town, Tehsil Khanpur, District Rahim Yar Khan', NULL, NULL, 1),
  (59, N'Khadija-tul-Kubra Campus', N'khadija-tul-kubra-campus', N'Maroot', N'maroot', N'Bahawalnagar', N'bahawalnagar', N'Punjab', N'Usama Town', N'usama-town', N'Sabzi Mandi Road, Usama Town, Maroot, Tehsil Fort Abbas', NULL, NULL, 1),
  (60, N'Hussain Campus', N'hussain-campus', N'Chak Bedi', N'chak-bedi', N'Pakpattan', N'pakpattan', N'Punjab', N'Chowk Sikandar', N'chowk-sikandar', N'Chowk Sikandar, Bonga Hayat Road, PO Chak Bedi, Pakpattan', NULL, NULL, 1),
  (61, N'Khalid Bin Waleed Campus, Bissian', N'khalid-bin-waleed-campus-bissian', N'Balakot', N'balakot', N'Mansehra', N'mansehra', N'Khyber Pakhtunkhwa', N'Bissian', N'bissian', N'Bissian, Tehsil Balakot, District Mansehra', NULL, NULL, 1),
  (62, N'Kala Bagh Campus', N'kala-bagh-campus', N'Abbottabad', N'abbottabad', N'Abbottabad', N'abbottabad', N'Khyber Pakhtunkhwa', N'Kala Bagh', N'kala-bagh', N'Kala Bagh, Barmi Gali, Abbottabad', NULL, NULL, 1),
  (63, N'Pir Mahal Campus, Sandhilianwali', N'pir-mahal-campus-sandhilianwali', N'Sandhilianwali', N'sandhilianwali', N'Toba Tek Singh', N'toba-tek-singh', N'Punjab', N'Abdul Hakeem Road', N'abdul-hakeem-road', N'Bhatti House, Near Dr. Mustafa Clinic, Abdul Hakeem Road, Sandhilianwali', NULL, NULL, 0),
  (64, N'Abu Huraira Campus, Multan', N'abu-huraira-campus-multan', N'Multan', N'multan', N'Multan', N'multan', N'Punjab', N'Qadirpur Raan', N'qadirpur-raan', N'Qadirpur Raan, Tehsil and District Multan', N'061-457888', NULL, 1),
  (65, N'Ifraheem Campus', N'ifraheem-campus', N'Chichawatni', N'chichawatni', N'Sahiwal', N'sahiwal', N'Punjab', N'Ward No. 19', N'ward-no-19', N'House No. 29, Street No. 1, Ward No. 19, Chichawatni, District Sahiwal', NULL, NULL, 1),
  (66, N'Abbottabad Campus', N'abbottabad-campus', N'Abbottabad', N'abbottabad', N'Abbottabad', N'abbottabad', N'Khyber Pakhtunkhwa', N'Mandian', N'mandian', N'Nara Road, Mandian, Near UET, Abbottabad', NULL, NULL, 1),
  (67, N'Mansehra Campus', N'mansehra-campus', N'Mansehra', N'mansehra', N'Mansehra', N'mansehra', N'Khyber Pakhtunkhwa', N'Abbottabad Road', N'abbottabad-road', N'Abbottabad Road, Chinnai, Near Saadi CNG, Mansehra', NULL, NULL, 1),
  (68, N'Rajana Campus', N'rajana-campus', N'Rajana', N'rajana', N'Toba Tek Singh', N'toba-tek-singh', N'Punjab', N'Model Town', N'model-town', N'Model Town, Pir Mahal Road, Rajana', NULL, NULL, 1),
  (69, N'Jawad Club Campus', N'jawad-club-campus', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'Walipura', N'walipura', N'Bazar No. 2, Walipura, Narwala Road, Faisalabad', NULL, NULL, 1),
  (70, N'Bashir Campus', N'bashir-campus', N'Gojra', N'gojra', N'Toba Tek Singh', N'toba-tek-singh', N'Punjab', N'Jhang Road', N'jhang-road', N'Bobak Chowk, Jhang Road, Gojra', NULL, NULL, 1),
  (71, N'Al Rehman Campus', N'al-rehman-campus', N'Burewala', N'burewala', N'Vehari', N'vehari', N'Punjab', N'Vehari Road', N'vehari-road', N'Near PI Link, Vehari Road, Burewala', NULL, NULL, 1),
  (72, N'Suleman Campus', N'suleman-campus', N'Fatyana', N'fatyana', N'Sahiwal', N'sahiwal', N'Punjab', N'Adda Daad', N'adda-daad', N'Adda Daad, Fatyana, Tehsil Chichawatni, District Sahiwal', N'0405-312248', NULL, 1),
  (73, N'Al Fareed Campus', N'al-fareed-campus', N'Sahiwal', N'sahiwal', N'Sahiwal', N'sahiwal', N'Punjab', N'Railway Road', N'railway-road', N'Railway Road, Sahiwal', NULL, NULL, 1),
  (74, N'Fatima Campus, Mongi Bangla', N'fatima-campus-mongi-bangla', N'Gojra', N'gojra', N'Toba Tek Singh', N'toba-tek-singh', N'Punjab', N'Adda Mongi Bangla', N'adda-mongi-bangla', N'Adda Mongi Bangla, Chak No. 160 GB, Tehsil Gojra, District Toba Tek Singh', NULL, NULL, 1),
  (75, N'Gatt Wala Campus', N'gatt-wala-campus', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'Gatt Wala', N'gatt-wala', N'199 RB, Officer Colony, Gatt Wala, Faisalabad', NULL, NULL, 1),
  (76, N'Nawan Shehr Campus', N'nawan-shehr-campus', N'Abbottabad', N'abbottabad', N'Abbottabad', N'abbottabad', N'Khyber Pakhtunkhwa', N'Nawan Shehr', N'nawan-shehr', N'Thanda Choa Road, Abbasia Colony, Nawan Shehr, Abbottabad Cantt', NULL, NULL, 1),
  (77, N'Ahmed Campus, Dahranwala', N'ahmed-campus-dahranwala', N'Dahranwala', N'dahranwala', N'Bahawalnagar', N'bahawalnagar', N'Punjab', N'Jaja Town', N'jaja-town', N'Jaja Town, Dahranwala, District Bahawalnagar', NULL, NULL, 1),
  (78, N'Aneeq Campus', N'aneeq-campus', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'Gujar Singhwala', N'gujar-singhwala', N'70 RB, Gujar Singhwala, Main Sheikhupura Road, Faisalabad', NULL, NULL, 1),
  (79, N'Muhammad Campus, Al Haram Society', N'muhammad-campus-al-haram-society', N'Okara', N'okara', N'Okara', N'okara', N'Punjab', N'Al Haram Society', N'al-haram-society', N'Al Haram Society, 49-2-L, Okara', NULL, NULL, 1),
  (80, N'Abu Huraira Campus, Okara', N'abu-huraira-campus-okara', N'Okara', N'okara', N'Okara', N'okara', N'Punjab', N'Zafar Colony', N'zafar-colony', N'Street No. 5, Zafar Colony, Chungi No. 7, GT Road, Okara', NULL, NULL, 1),
  (81, N'Sargodha Road Campus', N'sargodha-road-campus', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'Sargodha Road', N'sargodha-road', N'P-430/1, Eden Orchard Road, Lasani Pulli, Sargodha Road, Faisalabad', N'041-8846218', NULL, 1),
  (82, N'Tariq Bin Ziad Campus', N'tariq-bin-ziad-campus', N'Sahiwal', N'sahiwal', N'Sahiwal', N'sahiwal', N'Punjab', N'Tariq Bin Ziad Colony', N'tariq-bin-ziad-colony', N'Tariq Bin Ziad Colony, Sahiwal', NULL, NULL, 1),
  (83, N'Hamza Campus', N'hamza-campus', N'Chund Bharwana', N'chund-bharwana', N'Jhang', N'jhang', N'Punjab', N'Main Bazar', N'main-bazar', N'Main Bazar, Chund Bharwana, Sargodha Road, Jhang', N'0342-1444404', NULL, 1),
  (84, N'Zahid Campus', N'zahid-campus', N'Sahiwal', N'sahiwal', N'Sahiwal', N'sahiwal', N'Punjab', N'Harappa Road', N'harappa-road', N'150-9-L, Harappa Road, Sahiwal', NULL, NULL, 1),
  (85, N'Lal Sohanra Campus', N'lal-sohanra-campus', N'Lal Sohanra', N'lal-sohanra', N'Bahawalpur', N'bahawalpur', N'Punjab', N'Hasilpur Road', N'hasilpur-road', N'Street No. 3, Near Rural Hospital, 30 km, Lal Sohanra, Hasilpur Road, District Bahawalpur', NULL, NULL, 1),
  (86, N'Ghaziabad Campus', N'ghaziabad-campus', N'Chichawatni', N'chichawatni', N'Sahiwal', N'sahiwal', N'Punjab', N'Adda Ghaziabad', N'adda-ghaziabad', N'Gulshan-e-Noor Housing Society, Adda Ghaziabad, Tehsil Chichawatni, District Sahiwal', NULL, NULL, 1),
  (87, N'Muslim Campus, Dayal Garh', N'muslim-campus-dayal-garh', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'Dayal Garh', N'dayal-garh', N'115 JB, Phallai Chowk, Dayal Garh, Tehsil and District Faisalabad', NULL, NULL, 1),
  (88, N'City Campus, Abbottabad', N'city-campus-abbottabad', N'Abbottabad', N'abbottabad', N'Abbottabad', N'abbottabad', N'Khyber Pakhtunkhwa', N'Chinar Road', N'chinar-road', N'Chinar Road, Abbottabad', NULL, NULL, 1),
  (89, N'City Campus, Kanjwani', N'city-campus-kanjwani', N'Samundri', N'samundri', N'Faisalabad', N'faisalabad', N'Punjab', N'Kanjwani', N'kanjwani', N'Kanjwani City, 2 km Rahmay Shah, Near PSO Petrol Pump, Tehsil Samundri, District Faisalabad', NULL, NULL, 1),
  (90, N'Umar Campus, Okara', N'umar-campus-okara', N'Okara', N'okara', N'Okara', N'okara', N'Punjab', N'Saad City', N'saad-city', N'Saad City, Near Ameer Colony, Okara', NULL, NULL, 1),
  (91, N'Umar Campus, Sahianwala', N'umar-campus-sahianwala', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'Sahianwala', N'sahianwala', N'158 RB, Sahianwala, Faisalabad', NULL, NULL, 1),
  (92, N'Dawood Campus, Gaggoo Mandi', N'dawood-campus-gaggoo-mandi', N'Burewala', N'burewala', N'Vehari', N'vehari', N'Punjab', N'Gaggoo Mandi', N'gaggoo-mandi', N'Gaggoo Mandi, Near Shell Petrol Pump, Adda Quarter, Tehsil Burewala', NULL, NULL, 1),
  (93, N'Lower Canal Campus', N'lower-canal-campus', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'Jaranwala Road', N'jaranwala-road', N'Jaranwala Road, Faisalabad', NULL, NULL, 1),
  (94, N'Usman Abad Campus', N'usman-abad-campus', N'Abbottabad', N'abbottabad', N'Abbottabad', N'abbottabad', N'Khyber Pakhtunkhwa', N'Usman Abad', N'usman-abad', N'Usman Abad, Abbottabad', NULL, NULL, 1);

-- Regions
INSERT INTO dbo.regions (name, slug, sort_order)
SELECT DISTINCT x.region, CASE x.region WHEN N'Punjab' THEN N'punjab' ELSE N'khyber-pakhtunkhwa' END, 0
FROM @c x
WHERE NOT EXISTS (SELECT 1 FROM dbo.regions r WHERE r.slug = CASE x.region WHEN N'Punjab' THEN N'punjab' ELSE N'khyber-pakhtunkhwa' END);

-- Districts
INSERT INTO dbo.districts (region_id, name, slug, sort_order)
SELECT r.id, d.district, d.district_slug, 0
FROM (SELECT DISTINCT district, district_slug, region FROM @c) d
JOIN dbo.regions r ON r.slug = CASE d.region WHEN N'Punjab' THEN N'punjab' ELSE N'khyber-pakhtunkhwa' END
WHERE NOT EXISTS (SELECT 1 FROM dbo.districts x WHERE x.slug = d.district_slug);

-- Cities (an existing city, such as Faisalabad, is used as it is)
INSERT INTO dbo.cities (region_id, district_id, name, slug, sort_order)
SELECT d.region_id, d.id, c.city, c.city_slug, 0
FROM (SELECT DISTINCT city, city_slug, district_slug FROM @c) c
JOIN dbo.districts d ON d.slug = c.district_slug
WHERE NOT EXISTS (SELECT 1 FROM dbo.cities x WHERE x.slug = c.city_slug OR x.name = c.city);

-- Existing cities that have no district or region yet (for example added in the admin panel) get them,
-- so their campuses appear under the district filter. Backed up first.
IF OBJECT_ID(N'archive.cities_20261103') IS NULL
    SELECT * INTO archive.cities_20261103 FROM dbo.cities;
UPDATE ci SET district_id = COALESCE(ci.district_id, d.id), region_id = COALESCE(ci.region_id, d.region_id), updated_at = SYSDATETIMEOFFSET()
FROM dbo.cities ci
JOIN (SELECT DISTINCT city, city_slug, district_slug FROM @c) c ON ci.slug = c.city_slug OR ci.name = c.city
JOIN dbo.districts d ON d.slug = c.district_slug
WHERE ci.district_id IS NULL OR ci.region_id IS NULL;

-- Campuses
INSERT INTO dbo.branches (city_id, name, slug, area, area_slug, address_line, phone, phone_alt, status,
                          is_published, published_at, admission_status, sort_order)
SELECT ci.id, c.name, c.slug, c.area, c.area_slug, c.address, c.phone, c.phone_alt, N'active',
       c.published, CASE WHEN c.published = 1 THEN SYSDATETIMEOFFSET() END, N'contact_campus', 100 + c.row_no
FROM @c c
CROSS APPLY (SELECT TOP 1 x.id FROM dbo.cities x WHERE x.slug = c.city_slug OR x.name = c.city
             ORDER BY CASE WHEN x.slug = c.city_slug THEN 0 ELSE 1 END, x.created_at) ci
-- Skipped when the campus is already there, by web address or by name (it may have been added in the admin panel).
WHERE NOT EXISTS (SELECT 1 FROM dbo.branches b WHERE b.city_id = ci.id AND (b.slug = c.slug OR b.name = c.name) AND b.deleted_at IS NULL);

INSERT INTO dbo.schema_migrations (id) VALUES (N'10_faisalabad_region_campuses');
COMMIT TRANSACTION;
PRINT N'10_faisalabad_region_campuses applied.';
GO
SET NOEXEC OFF;
GO
