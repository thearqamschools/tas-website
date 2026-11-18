-- =============================================================================
-- THE ARQAM SCHOOLS: CAMPUSES FROM THE "ADMISSION OPEN" FLYER (18 November 2026)
-- Run after 19. Safe to run twice: it records itself in dbo.schema_migrations.
--
-- Source: the printed "Admission Open" flyer (campus list "Visit us here"), sent by the owner.
-- The flyer's campuses were compared with the campuses already on the website (Faisalabad
-- region, Database/10). Only campuses that are NOT on the website yet are added: 117 campuses
-- in Lahore, Sheikhupura, Nankana Sahib, Kasur, Hafizabad, Rawalpindi, Islamabad, Khushab, Gujrat,
-- Chakwal, Gujranwala, Narowal, Sialkot, Sargodha, Karachi and some in the Faisalabad region.
-- Only the address and the phone number printed on the flyer are used. Campus names are made
-- from the area (for example "Johar Town Campus") and can be changed in the admin panel.
-- 6 campuses whose details could not be read clearly or need the owner's confirmation are
-- added HIDDEN (not on the website) until they are checked in the admin panel.
-- New regions (Islamabad Capital Territory, Sindh), districts and cities are added when missing.
-- Nothing is deleted; a campus that already exists (same city and web address or name) is skipped.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF EXISTS (SELECT 1 FROM dbo.schema_migrations WHERE id = N'20_flyer_campuses')
BEGIN
    PRINT N'20_flyer_campuses is already applied. Nothing to do.';
    SET NOEXEC ON;
END
GO
BEGIN TRANSACTION;

DECLARE @c TABLE (row_no INT, name NVARCHAR(150), slug NVARCHAR(120), city NVARCHAR(100), city_slug NVARCHAR(120),
                  district NVARCHAR(100), district_slug NVARCHAR(120), region NVARCHAR(100), region_slug NVARCHAR(120),
                  area NVARCHAR(120), area_slug NVARCHAR(120), address NVARCHAR(300), phone NVARCHAR(20), published BIT);
INSERT INTO @c VALUES
  (40, N'Ram Dewali Campus', N'ram-dewali-campus', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'punjab', N'Ram Dewali', N'ram-dewali', N'2 Chak, Ram Dewali, Sargodha Road, Faisalabad', N'0302-3444414', 1),
  (43, N'Chak 208 RB Campus', N'chak-208-rb-campus', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'punjab', N'Chak 208 RB', N'chak-208-rb', N'Chak No. 208 RB, Faisalabad', N'0301-7001141', 1),
  (19, N'Chak 72 Gadian Campus', N'chak-72-gadian-campus', N'Faisalabad', N'faisalabad', N'Faisalabad', N'faisalabad', N'Punjab', N'punjab', N'Satiana Road', N'satiana-road', N'Chak 72 Gadian, Satiana Road, Faisalabad', N'0306-4210506', 1),
  (21, N'Satiana Campus', N'satiana-campus', N'Satiana', N'satiana', N'Faisalabad', N'faisalabad', N'Punjab', N'punjab', N'Jhamra Road', N'jhamra-road', N'39 GB, Jhamra Road, Satiana', N'0306-4564690', 1),
  (48, N'Naya Lahore Campus', N'naya-lahore-campus', N'Naya Lahore', N'naya-lahore', N'Toba Tek Singh', N'toba-tek-singh', N'Punjab', N'punjab', N'Naya Lahore', N'naya-lahore', N'Naya Lahore, District Toba Tek Singh', N'0321-4065651', 1),
  (53, N'Girls Campus, Chiniot', N'girls-campus-chiniot', N'Chiniot', N'chiniot', N'Chiniot', N'chiniot', N'Punjab', N'punjab', N'Satellite Town', N'satellite-town', N'Asghar Chowk, Satellite Town, Chiniot', N'0321-7702271', 1),
  (54, N'Khanewal Campus', N'khanewal-campus', N'Khanewal', N'khanewal', N'Khanewal', N'khanewal', N'Punjab', N'punjab', N'Nazim Abad Puli', N'nazim-abad-puli', N'Nazim Abad Puli, Main GT Road, Khanewal', N'0305-7736104', 1),
  (57, N'Kacha Khoo Campus', N'kacha-khoo-campus', N'Kacha Khoo', N'kacha-khoo', N'Khanewal', N'khanewal', N'Punjab', N'punjab', N'Fateh Town', N'fateh-town', N'Fateh Town, Main GT Road, Adda Kacha Khoo', N'0305-7736104', 1),
  (65, N'Renala Khurd Campus', N'renala-khurd-campus', N'Renala Khurd', N'renala-khurd', N'Okara', N'okara', N'Punjab', N'punjab', N'Adda Chucak', N'adda-chucak', N'Adda Chucak, Tehsil Renala Khurd, District Okara', N'0345-7491072', 1),
  (68, N'Lala Zar Colony Campus', N'lala-zar-colony-campus', N'Sahiwal', N'sahiwal', N'Sahiwal', N'sahiwal', N'Punjab', N'punjab', N'Lala Zar Colony', N'lala-zar-colony', N'House No. 4 West, Chowk Lala Zar Colony, Sahiwal', N'0346-7463845', 1),
  (70, N'Noor Shah Campus', N'noor-shah-campus', N'Noor Shah', N'noor-shah', N'Sahiwal', N'sahiwal', N'Punjab', N'punjab', N'Noor Shah', N'noor-shah', N'Noor Shah, Tehsil and District Sahiwal', N'0345-7491072', 1),
  (77, N'Shah Shamas Colony Campus', N'shah-shamas-colony-campus', N'Multan', N'multan', N'Multan', N'multan', N'Punjab', N'punjab', N'New Shah Shamas Colony', N'new-shah-shamas-colony', N'Near Sui Gas Chowk, New Shah Shamas Colony, Multan', N'0345-7200782', 1),
  (83, N'Garhi Shahu Campus', N'garhi-shahu-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Garhi Shahu', N'garhi-shahu', N'97 Fazal Street, Allama Iqbal Road, Garhi Shahu, Lahore', N'0333-4121418', 1),
  (84, N'Angoori Bagh Campus', N'angoori-bagh-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Angoori Bagh', N'angoori-bagh', N'19/10 Angoori Bagh Scheme No. 2, Shalimar Link Road, Lahore', N'0321-4765797', 1),
  (85, N'Gulshan-e-Ravi Campus', N'gulshan-e-ravi-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Gulshan-e-Ravi', N'gulshan-e-ravi', N'11-F Main Boulevard, Near Meezan Bank, Gulshan-e-Ravi, Lahore', N'0333-8411149', 1),
  (86, N'Allama Iqbal Town Campus', N'allama-iqbal-town-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Allama Iqbal Town', N'allama-iqbal-town', N'349 Sutlej Block, Allama Iqbal Town, Lahore', N'0321-8454633', 1),
  (87, N'Rehman Pura Campus', N'rehman-pura-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Rehman Pura', N'rehman-pura', N'1-B Rehman Pura, Lahore', N'0321-7101010', 1),
  (88, N'PCSIR Staff Colony Campus', N'pcsir-staff-colony-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'PCSIR Staff Colony', N'pcsir-staff-colony', N'22-D PCSIR Staff Colony, Near College Road, Lahore', N'0300-9487888', 1),
  (89, N'Faisal Town Campus', N'faisal-town-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Faisal Town', N'faisal-town', N'1-C, Opposite NESPAK Office, Faisal Town, Lahore', N'0308-4077202', 1),
  (90, N'Nishat Colony Campus', N'nishat-colony-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Nishat Colony', N'nishat-colony', N'17/3 Akhri Stop, Nishat Colony, Lahore', N'0300-8452294', 1),
  (91, N'Salamatpura Campus', N'salamatpura-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Salamatpura', N'salamatpura', N'Nayab Park, Near Salamatpura Metro Station, G.T. Road, Lahore', N'0321-4387645', 1),
  (92, N'Township Campus', N'township-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Township', N'township', N'314 A-I Township, Lahore', NULL, 0),
  (93, N'Awaisia Housing Society Campus', N'awaisia-housing-society-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Awaisia Housing Society', N'awaisia-housing-society', N'Awaisia Housing Society, College Road, Lahore', N'0321-6012211', 1),
  (94, N'Mughalpura Campus', N'mughalpura-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Mughalpura', N'mughalpura', N'House No. 5, Street No. 5-B, Shah Kamal Road, Shalimar Link Road, Mughalpura, Lahore', N'0321-9496975', 1),
  (95, N'Sabzazar Campus', N'sabzazar-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Sabzazar', N'sabzazar', N'10-E Sabzazar Scheme, Lahore', N'0301-4134244', 1),
  (96, N'Mahmood Booti Campus', N'mahmood-booti-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Mahmood Booti', N'mahmood-booti', N'16-DS, Salahuddin Road, Mahmood Booti, Lahore', N'0300-4037505', 1),
  (97, N'Riwaz Garden Campus', N'riwaz-garden-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Riwaz Garden', N'riwaz-garden', N'69 Riwaz Garden, Main Sanda Road, Near MAO College, Lahore', N'0321-4722009', 1),
  (98, N'Jubilee Town Campus', N'jubilee-town-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Jubilee Town', N'jubilee-town', N'129-E, Jubilee Town, Canal Bank Road, Lahore', N'0324-4071989', 1),
  (99, N'Shadab Colony Campus', N'shadab-colony-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Ferozepur Road', N'ferozepur-road', N'1993-D, Shadab Colony Road, 18 km Ferozepur Road, Lahore', N'0321-8015888', 1),
  (100, N'Marghazar Colony Campus', N'marghazar-colony-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Marghazar Colony', N'marghazar-colony', N'16-P Marghazar Colony, Multan Road, Lahore', N'0321-4967750', 1),
  (101, N'Sultan Pura Campus', N'sultan-pura-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Sultan Pura', N'sultan-pura', N'Ali Cottage, Opposite Shaafi Masjid, Main Ghorray Shah Road, Sultan Pura, Lahore', N'0333-4246404', 1),
  (102, N'Libya Town Campus', N'libya-town-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Raiwind Road', N'raiwind-road', N'Libya Town, Gia Bagga Road, Arraiyan, Off Raiwind Road, Lahore', N'0315-4164936', 1),
  (103, N'Johar Town Campus', N'johar-town-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Johar Town', N'johar-town', N'1-H/I Johar Town, Lahore', N'0300-8403366', 1),
  (104, N'New Mozang Campus', N'new-mozang-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'New Mozang', N'new-mozang', N'Building No. 18, Jehangeer Park, New Mozang, Lahore', N'0332-8075412', 1),
  (105, N'Shahdara Campus', N'shahdara-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Shahdara', N'shahdara', N'Aslam Park, Main Bus Stop, Shahdara Town, Lahore', N'0336-4122468', 1),
  (106, N'Zubaida Park Campus', N'zubaida-park-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Chowk Yateem Khana', N'chowk-yateem-khana', N'Zubaida Park, Chowk Yateem Khana, Lahore', N'0321-6662637', 1),
  (107, N'Khuda Bukhsh Society Campus', N'khuda-bukhsh-society-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Airport Road', N'airport-road', N'51-T Khuda Bukhsh Housing Society, New Airport Road, Lahore Cantt', N'0300-4441780', 1),
  (108, N'Wahdat Road Campus', N'wahdat-road-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Wahdat Road', N'wahdat-road', N'58 Fruit Mandi, Kamran Block, Allama Iqbal Town, Wahdat Road, Lahore', N'0321-9225822', 1),
  (109, N'Iqra Campus, Awan Town', N'iqra-campus-awan-town', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Awan Town', N'awan-town', N'108-C, Madina Block, Awan Town, Lahore', N'0333-4251057', 1),
  (110, N'Daroghawala Campus', N'daroghawala-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Daroghawala', N'daroghawala', N'Near Nadia Ghee Mills, Akhri Mint Stop, Daroghawala, Lahore', N'0323-4164282', 1),
  (111, N'Chung Campus', N'chung-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Chung', N'chung', N'Madina Colony, Chung, Multan Road, Lahore', N'0321-9225822', 1),
  (112, N'Ichhra Campus', N'ichhra-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Ichhra', N'ichhra', N'Near Ariyan Masjid, Dhobian Street, Peer Ghazi Road, Ichhra, Lahore', N'0321-4464831', 1),
  (113, N'Qila Gujjar Singh Campus', N'qila-gujjar-singh-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Qila Gujjar Singh', N'qila-gujjar-singh', N'House No. 5, Street No. 3, Qila Gujjar Singh, Lahore', N'0333-4121418', 1),
  (114, N'Gawalmandi Campus', N'gawalmandi-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Gawalmandi', N'gawalmandi', N'Main Bazar, Gawalmandi, Lahore', N'0333-4121418', 1),
  (115, N'Habib Park Campus', N'habib-park-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Multan Road', N'multan-road', N'103 Habib Park, Multan Road, Lahore', N'0300-4532394', 1),
  (116, N'Bhagatpura Campus', N'bhagatpura-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Bhagatpura', N'bhagatpura', N'Mehar Tufail Road, Near Majid Shadi Hall, Bhagatpura, Lahore', N'0322-4617858', 1),
  (117, N'Ittefaq Town Campus', N'ittefaq-town-campus', N'Lahore', N'lahore', N'Lahore', N'lahore', N'Punjab', N'punjab', N'Ittefaq Town', N'ittefaq-town', N'Chairman Plaza, Ittefaq Town, Lahore', N'042-35461611', 1),
  (118, N'Buchiki Campus', N'buchiki-campus', N'Buchiki', N'buchiki', N'Nankana Sahib', N'nankana-sahib', N'Punjab', N'punjab', N'Buchiki', N'buchiki', N'Near Govt. Iqbal Model School, Buchiki, District Nankana Sahib (also listed: Aala Campus, Bhara Ghar)', N'0321-4243444', 0),
  (119, N'Sheikhupura Campus', N'sheikhupura-campus', N'Sheikhupura', N'sheikhupura', N'Sheikhupura', N'sheikhupura', N'Punjab', N'punjab', N'PSO Pump', N'pso-pump', N'Behind Yousaf Qureshi Hospital, Near PSO Pump, Sheikhupura', N'0300-4400877', 1),
  (120, N'Manawala Campus', N'manawala-campus', N'Manawala', N'manawala', N'Sheikhupura', N'sheikhupura', N'Punjab', N'punjab', N'Main Bazar', N'main-bazar', N'Awan Market, Main Bazar, Manawala, District Sheikhupura', N'056-3771717', 1),
  (121, N'Ferozwala Campus', N'ferozwala-campus', N'Ferozwala', N'ferozwala', N'Sheikhupura', N'sheikhupura', N'Punjab', N'punjab', N'People''s Colony', N'peoples-colony', N'People''s Colony, Main Bazar, Near Police Station, Ferozwala, District Sheikhupura', N'0334-8061124', 1),
  (122, N'Farooqabad Campus', N'farooqabad-campus', N'Farooqabad', N'farooqabad', N'Sheikhupura', N'sheikhupura', N'Punjab', N'punjab', N'Sargodha Road', N'sargodha-road', N'Sargodha Road, Farooqabad, District Sheikhupura', N'0333-6533590', 1),
  (123, N'Khanqah Dogran Campus', N'khanqah-dogran-campus', N'Khanqah Dogran', N'khanqah-dogran', N'Sheikhupura', N'sheikhupura', N'Punjab', N'punjab', N'Sargodha Road', N'sargodha-road', N'Sargodha Road, Khanqah Dogran, District Sheikhupura', N'0300-9842409', 1),
  (124, N'Johal Adda Campus', N'johal-adda-campus', N'Johal Adda', N'johal-adda', N'Faisalabad', N'faisalabad', N'Punjab', N'punjab', N'Sheikhupura Road', N'sheikhupura-road', N'Johal Adda, Main Sheikhupura Road, District Faisalabad', N'0333-4282084', 1),
  (125, N'Bhikhi Campus', N'bhikhi-campus', N'Bhikhi', N'bhikhi', N'Sheikhupura', N'sheikhupura', N'Punjab', N'punjab', N'Bhikhi', N'bhikhi', N'Near Govt. Girls High School, Bhikhi, District Sheikhupura', N'0333-4326578', 1),
  (126, N'Housing Colony Campus', N'housing-colony-campus', N'Sheikhupura', N'sheikhupura', N'Sheikhupura', N'sheikhupura', N'Punjab', N'punjab', N'Housing Colony', N'housing-colony', N'15-Y Housing Colony, Main Lahore Road, Sheikhupura', N'0300-8854514', 1),
  (127, N'Roshan Canal Gardens Campus', N'roshan-canal-gardens-campus', N'Ferozwala', N'ferozwala', N'Sheikhupura', N'sheikhupura', N'Punjab', N'punjab', N'Roshan Canal Gardens', N'roshan-canal-gardens', N'Roshan Canal Gardens, Khanpur, Tehsil Ferozwala, District Sheikhupura', N'0333-4471240', 1),
  (128, N'Nankana Sahib Campus', N'nankana-sahib-campus', N'Nankana Sahib', N'nankana-sahib', N'Nankana Sahib', N'nankana-sahib', N'Punjab', N'punjab', N'Shadbagh Colony', N'shadbagh-colony', N'Shadbagh Colony, Tehsil and District Nankana Sahib', N'0303-7777778', 1),
  (129, N'Manawala Campus 2', N'manawala-campus-2', N'Manawala', N'manawala', N'Sheikhupura', N'sheikhupura', N'Punjab', N'punjab', N'Main Bazar', N'main-bazar', N'Awan Market, Main Bazar, Manawala', N'0300-8689604', 0),
  (130, N'Warburton Campus', N'warburton-campus', N'Warburton', N'warburton', N'Nankana Sahib', N'nankana-sahib', N'Punjab', N'punjab', N'By Pass Road', N'by-pass-road', N'By Pass Road, Near United Hospital, Warburton, Tehsil and District Nankana Sahib', N'0332-4007947', 1),
  (131, N'Safdarabad Campus', N'safdarabad-campus', N'Safdarabad', N'safdarabad', N'Sheikhupura', N'sheikhupura', N'Punjab', N'punjab', N'Ghalla Mandi', N'ghalla-mandi', N'Ghalla Mandi, Near HBL, Safdarabad, District Sheikhupura', N'0300-4658043', 1),
  (132, N'Sharaqpur Road Campus', N'sharaqpur-road-campus', N'Sheikhupura', N'sheikhupura', N'Sheikhupura', N'sheikhupura', N'Punjab', N'punjab', N'Sharaqpur Road', N'sharaqpur-road', N'Sharaqpur Road, Sheikhupura', N'0300-9652651', 1),
  (133, N'Pattoki Campus', N'pattoki-campus', N'Pattoki', N'pattoki', N'Kasur', N'kasur', N'Punjab', N'punjab', N'Garden Town', N'garden-town', N'Kasur Road, Opposite Dildar Mahboob Hospital, Garden Town, Pattoki, District Kasur', N'0343-4754462', 1),
  (134, N'Halla Campus', N'halla-campus', N'Halla', N'halla', N'Kasur', N'kasur', N'Punjab', N'punjab', N'Chuchuk Road', N'chuchuk-road', N'Near Police Post, Chuchuk Road, Halla, Tehsil Pattoki, District Kasur', N'0343-4754462', 1),
  (135, N'Hafizabad Campus', N'hafizabad-campus', N'Hafizabad', N'hafizabad', N'Hafizabad', N'hafizabad', N'Punjab', N'punjab', N'Sagar Road', N'sagar-road', N'Near Bank Alfalah, Sagar Road, Hafizabad', N'0321-8524043', 1),
  (136, N'Upper Canal View Campus', N'upper-canal-view-campus', N'Hafizabad', N'hafizabad', N'Hafizabad', N'hafizabad', N'Punjab', N'punjab', N'Upper Canal View', N'upper-canal-view', N'Near Do-aaba Rice Mills, 777 Upper Canal View, Hafizabad', N'0321-8524043', 1),
  (137, N'Karachi Campus', N'karachi-campus', N'Karachi', N'karachi', N'Karachi', N'karachi', N'Sindh', N'sindh', N'Gulistan-e-Johar', N'gulistan-e-johar', N'B-12, Architect & Engineering Society, Block 8, Gulistan-e-Johar, Karachi', N'0333-4969284', 1),
  (138, N'Chaway Wala Campus', N'chaway-wala-campus', N'Chaway Wala', N'chaway-wala', N'Sargodha', N'sargodha', N'Punjab', N'punjab', N'Ahmedpur', N'ahmedpur', N'Chaway Wala, PO Box Ahmedpur, District Sargodha', N'0300-8609556', 1),
  (139, N'Silanwali Campus', N'silanwali-campus', N'Silanwali', N'silanwali', N'Sargodha', N'sargodha', N'Punjab', N'punjab', N'Garden Town', N'garden-town', N'Garden Town, Near WAPDA Office, Silanwali, District Sargodha', N'0345-8700122', 1),
  (140, N'Khayaban-e-Sir Syed Campus', N'khayaban-e-sir-syed-campus', N'Rawalpindi', N'rawalpindi', N'Rawalpindi', N'rawalpindi', N'Punjab', N'punjab', N'Khayaban-e-Sir Syed', N'khayaban-e-sir-syed', N'376-B, Street No. 5, Sector 1, Khayaban-e-Sir Syed, Rawalpindi', N'0333-6565673', 1),
  (141, N'Eidgah Road Campus', N'eidgah-road-campus', N'Rawalpindi', N'rawalpindi', N'Rawalpindi', N'rawalpindi', N'Punjab', N'punjab', N'Eidgah Road', N'eidgah-road', N'P-1023 Eidgah Road, Asghar Mall Chowk, Rawalpindi', N'0300-9658583', 1),
  (142, N'Lala Rukh Colony Campus', N'lala-rukh-colony-campus', N'Wah Cantt', N'wah-cantt', N'Rawalpindi', N'rawalpindi', N'Punjab', N'punjab', N'Lala Rukh Colony', N'lala-rukh-colony', N'95-A Lala Rukh Colony, Wah Cantt', N'0300-7907512', 1),
  (143, N'Gulistan Colony Campus, Wah Cantt', N'gulistan-colony-campus-wah-cantt', N'Wah Cantt', N'wah-cantt', N'Rawalpindi', N'rawalpindi', N'Punjab', N'punjab', N'Gulistan Colony', N'gulistan-colony', N'Gulistan Colony, Wah Cantt', N'0300-7907513', 1),
  (144, N'Barma Town Campus', N'barma-town-campus', N'Islamabad', N'islamabad', N'Islamabad', N'islamabad', N'Islamabad Capital Territory', N'islamabad-capital-territory', N'Barma Town', N'barma-town', N'Barma Town, Islamabad', N'0300-5552596', 1),
  (145, N'I-10 Campus', N'i-10-campus', N'Islamabad', N'islamabad', N'Islamabad', N'islamabad', N'Islamabad Capital Territory', N'islamabad-capital-territory', N'I-10/4', N'i-10-4', N'House No. 5, I-10/4, Islamabad', N'0336-5530521', 1),
  (146, N'Soan Garden Campus', N'soan-garden-campus', N'Islamabad', N'islamabad', N'Islamabad', N'islamabad', N'Islamabad Capital Territory', N'islamabad-capital-territory', N'Soan Garden', N'soan-garden', N'House No. 16, Block C, Doubal Road, Soan Garden, Islamabad', N'0300-5211523', 1),
  (147, N'Koral Campus', N'koral-campus', N'Islamabad', N'islamabad', N'Islamabad', N'islamabad', N'Islamabad Capital Territory', N'islamabad-capital-territory', N'Koral', N'koral', N'Ameen Town, Near Gulberg, Koral, Islamabad', N'0306-5118579', 1),
  (148, N'Pirwadhai Campus', N'pirwadhai-campus', N'Rawalpindi', N'rawalpindi', N'Rawalpindi', N'rawalpindi', N'Punjab', N'punjab', N'Pirwadhai', N'pirwadhai', N'Street No. 21, House No. ZB-361, Fauji Colony, Pirwadhai, Rawalpindi', N'0332-5000537', 1),
  (149, N'Nawaz Sharif Park Campus', N'nawaz-sharif-park-campus', N'Rawalpindi', N'rawalpindi', N'Rawalpindi', N'rawalpindi', N'Punjab', N'punjab', N'7th Road', N'7th-road', N'NA-446, 7th Road, Near Nawaz Sharif Park, Rawalpindi', N'0321-5836147', 1),
  (150, N'Mandi Bahauddin Campus', N'mandi-bahauddin-campus', N'Mandi Bahauddin', N'mandi-bahauddin', N'Mandi Bahauddin', N'mandi-bahauddin', N'Punjab', N'punjab', N'Rehmat Qila', N'rehmat-qila', N'Rehmat Qila, Gujran Wasu Road, Mandi Bahauddin', N'0331-7603643', 1),
  (151, N'Quaidabad Campus', N'quaidabad-campus', N'Quaidabad', N'quaidabad', N'Khushab', N'khushab', N'Punjab', N'punjab', N'F Block', N'f-block', N'68 F Block, Quaidabad, District Khushab', N'0301-6342254', 1),
  (152, N'Jauharabad Campus', N'jauharabad-campus', N'Jauharabad', N'jauharabad', N'Khushab', N'khushab', N'Punjab', N'punjab', N'New Satellite Town', N'new-satellite-town', N'12/18, New Satellite Town, Link Sarwar Shaheed Road, Jauharabad, District Khushab', N'0301-4177286', 1),
  (153, N'Soon Valley Campus', N'soon-valley-campus', N'Naushera', N'naushera', N'Khushab', N'khushab', N'Punjab', N'punjab', N'Soon Valley', N'soon-valley', N'Naushera, Soon Valley, District Khushab', N'0301-4802301', 1),
  (154, N'City Campus, Khushab', N'city-campus-khushab', N'Khushab', N'khushab', N'Khushab', N'khushab', N'Punjab', N'punjab', N'City', N'city', N'City Campus, Tehsil and District Khushab', N'0301-4177286', 1),
  (155, N'Noorpur Thal Campus', N'noorpur-thal-campus', N'Noorpur Thal', N'noorpur-thal', N'Khushab', N'khushab', N'Punjab', N'punjab', N'Lal Colony', N'lal-colony', N'Lal Colony, Noorpur Thal, Tehsil Noorpur, District Khushab', N'0302-4455723', 1),
  (156, N'Fatehpur Campus', N'fatehpur-campus', N'Fatehpur', N'fatehpur', N'Gujrat', N'gujrat', N'Punjab', N'punjab', N'Awan Shareef Road', N'awan-shareef-road', N'Awan Shareef Road, Fatehpur, District Gujrat', N'0300-6208614', 1),
  (157, N'Service Morr Campus', N'service-morr-campus', N'Gujrat', N'gujrat', N'Gujrat', N'gujrat', N'Punjab', N'punjab', N'Service Morr', N'service-morr', N'Sultan Market, Service Morr, Gujrat', N'0321-6240407', 1),
  (158, N'Dolat Nagar Campus', N'dolat-nagar-campus', N'Gujrat', N'gujrat', N'Gujrat', N'gujrat', N'Punjab', N'punjab', N'Dolat Nagar', N'dolat-nagar', N'Bhimber Road, Dolat Nagar, Tehsil and District Gujrat', N'0344-6248673', 1),
  (159, N'Karianwala Campus', N'karianwala-campus', N'Karianwala', N'karianwala', N'Gujrat', N'gujrat', N'Punjab', N'punjab', N'Jalalpur Jattan Road', N'jalalpur-jattan-road', N'Jalalpur Jattan Road, Karianwala, Tehsil and District Gujrat', N'0300-6280047', 1),
  (160, N'Qamar Sialvi Road Campus', N'qamar-sialvi-road-campus', N'Gujrat', N'gujrat', N'Gujrat', N'gujrat', N'Punjab', N'punjab', N'Qamar Sialvi Road', N'qamar-sialvi-road', N'Qamar Sialvi Road, Near NBP, Gujrat', N'0302-9646860', 1),
  (161, N'Kunjah Road Campus', N'kunjah-road-campus', N'Gujrat', N'gujrat', N'Gujrat', N'gujrat', N'Punjab', N'punjab', N'Kunjah Road', N'kunjah-road', N'Shaheen Chowk, Kunjah Road, Near Noori Masjid', N'0307-6499311', 0),
  (162, N'The Arqam College, Narang Mandi', N'the-arqam-college-narang-mandi', N'Narang Mandi', N'narang-mandi', N'Sheikhupura', N'sheikhupura', N'Punjab', N'punjab', N'Sadar Bazar', N'sadar-bazar', N'Mandi Sadar Bazar, Narang Mandi', N'0341-0041804', 0),
  (163, N'Allied Park Campus', N'allied-park-campus', N'Chakwal', N'chakwal', N'Chakwal', N'chakwal', N'Punjab', N'punjab', N'Allied Park', N'allied-park', N'Allied Park, Chakwal', N'0332-5781640', 1),
  (164, N'Choa Saidan Shah Campus', N'choa-saidan-shah-campus', N'Choa Saidan Shah', N'choa-saidan-shah', N'Chakwal', N'chakwal', N'Punjab', N'punjab', N'Choa Saidan Shah', N'choa-saidan-shah', N'Choa Saidan Shah, District Chakwal', N'0345-5775552', 1),
  (165, N'Girls College Road Campus', N'girls-college-road-campus', N'Chakwal', N'chakwal', N'Chakwal', N'chakwal', N'Punjab', N'punjab', N'Girls College Road', N'girls-college-road', N'Girls College Road, Near D. Watson, Chakwal', N'054-3692266', 1),
  (166, N'Kamoke Campus', N'kamoke-campus', N'Kamoke', N'kamoke', N'Gujranwala', N'gujranwala', N'Punjab', N'punjab', N'Khan Town', N'khan-town', N'Khan Town, Kamoke, District Gujranwala', N'0300-6435804', 1),
  (167, N'Sadhoke Campus', N'sadhoke-campus', N'Sadhoke', N'sadhoke', N'Gujranwala', N'gujranwala', N'Punjab', N'punjab', N'Sadhoke', N'sadhoke', N'Sadhoke, District Gujranwala', N'0324-6464534', 1),
  (168, N'Gondlanwala Campus', N'gondlanwala-campus', N'Gujranwala', N'gujranwala', N'Gujranwala', N'gujranwala', N'Punjab', N'punjab', N'Gondlanwala', N'gondlanwala', N'Alipur Road, Gondlanwala, District Gujranwala', N'0333-8605634', 1),
  (169, N'Kalaskay Campus', N'kalaskay-campus', N'Wazirabad', N'wazirabad', N'Gujranwala', N'gujranwala', N'Punjab', N'punjab', N'Kalaskay', N'kalaskay', N'Alipur Road, Village Kalaskay, Tehsil Wazirabad, District Gujranwala', N'0300-6421419', 1),
  (170, N'Arfat Colony Campus', N'arfat-colony-campus', N'Gujranwala', N'gujranwala', N'Gujranwala', N'gujranwala', N'Punjab', N'punjab', N'Arfat Colony', N'arfat-colony', N'House No. 1, Arfat Colony, Gujranwala', N'0333-8166058', 1),
  (171, N'Satellite Town Campus, Gujranwala', N'satellite-town-campus-gujranwala', N'Gujranwala', N'gujranwala', N'Gujranwala', N'gujranwala', N'Punjab', N'punjab', N'Satellite Town', N'satellite-town', N'Satellite Town, Gujranwala', N'0300-6317072', 1),
  (172, N'Model Town Campus, Gujranwala', N'model-town-campus-gujranwala', N'Gujranwala', N'gujranwala', N'Gujranwala', N'gujranwala', N'Punjab', N'punjab', N'Model Town', N'model-town', N'Model Town, Gujranwala', N'0315-8231315', 1),
  (173, N'Kanjroor Campus', N'kanjroor-campus', N'Kanjroor', N'kanjroor', N'Narowal', N'narowal', N'Punjab', N'punjab', N'Kanjroor', N'kanjroor', N'Kanjroor, District Narowal', N'0300-7766015', 1),
  (174, N'Qila Ahmadabad Campus', N'qila-ahmadabad-campus', N'Qila Ahmadabad', N'qila-ahmadabad', N'Narowal', N'narowal', N'Punjab', N'punjab', N'Qila Ahmadabad', N'qila-ahmadabad', N'Qila Ahmadabad, District Narowal', N'0300-7766015', 1),
  (175, N'Narowal Campus', N'narowal-campus', N'Narowal', N'narowal', N'Narowal', N'narowal', N'Punjab', N'punjab', N'Sialkot Phatak', N'sialkot-phatak', N'Sialkot Phatak, Near Total Petrol Pump, Narowal', N'0334-8246121', 1),
  (176, N'Shakargarh Campus', N'shakargarh-campus', N'Shakargarh', N'shakargarh', N'Narowal', N'narowal', N'Punjab', N'punjab', N'Darman Road', N'darman-road', N'Darman Road, Shakargarh', N'0300-7774847', 1),
  (177, N'Sambrial Campus', N'sambrial-campus', N'Sambrial', N'sambrial', N'Sialkot', N'sialkot', N'Punjab', N'punjab', N'Wazirabad-Sialkot Road', N'wazirabad-sialkot-road', N'Main Wazirabad-Sialkot Road, Sambrial, District Sialkot', N'0333-5497575', 1),
  (178, N'Kashmiri Bazar Campus', N'kashmiri-bazar-campus', N'Sialkot', N'sialkot', N'Sialkot', N'sialkot', N'Punjab', N'punjab', N'Kashmiri Bazar', N'kashmiri-bazar', N'Khan Bahadur Haveli, Opposite Sunehri Masjid, Kashmiri Bazar, Sialkot', N'0333-8632727', 1),
  (179, N'Begowala Campus', N'begowala-campus', N'Begowala', N'begowala', N'Sialkot', N'sialkot', N'Punjab', N'punjab', N'Begowala', N'begowala', N'Begowala, Tehsil Sambrial, District Sialkot', N'0345-6729028', 1),
  (180, N'Bharth Campus', N'bharth-campus', N'Sialkot', N'sialkot', N'Sialkot', N'sialkot', N'Punjab', N'punjab', N'Gondal Road', N'gondal-road', N'Gondal Road, Kalma Chowk, Near Fauji Foundation School, Bharth, Sialkot', N'0333-8661901', 1),
  (181, N'Klasswala Campus', N'klasswala-campus', N'Klasswala', N'klasswala', N'Sialkot', N'sialkot', N'Punjab', N'punjab', N'Sharifpura', N'sharifpura', N'Sharifpura, Klasswala, Tehsil Pasrur, District Sialkot', N'0332-4742425', 1),
  (182, N'Majra Kalan Campus', N'majra-kalan-campus', N'Majra Kalan', N'majra-kalan', N'Sialkot', N'sialkot', N'Punjab', N'punjab', N'Majra Kalan', N'majra-kalan', N'Majra Kalan, Tehsil Sambrial, District Sialkot', N'0321-9668587', 1),
  (183, N'Khadim Ali Road Campus', N'khadim-ali-road-campus', N'Sialkot', N'sialkot', N'Sialkot', N'sialkot', N'Punjab', N'punjab', N'Khadim Ali Road', N'khadim-ali-road', N'Khadim Ali Road, Sialkot', N'0333-8632727', 0),
  (184, N'Anwaar Club Campus', N'anwaar-club-campus', N'Sialkot', N'sialkot', N'Sialkot', N'sialkot', N'Punjab', N'punjab', N'Anwaar Club', N'anwaar-club', N'Near Anwaar Club, Sialkot', N'0332-8637566', 1),
  (185, N'Khrota Syedan Campus', N'khrota-syedan-campus', N'Sialkot', N'sialkot', N'Sialkot', N'sialkot', N'Punjab', N'punjab', N'Jinnah Road', N'jinnah-road', N'Jinnah Road, Khrota Syedan, Sialkot', N'0344-6337338', 1),
  (186, N'Pasrur Campus', N'pasrur-campus', N'Pasrur', N'pasrur', N'Sialkot', N'sialkot', N'Punjab', N'punjab', N'NADRA Office', N'nadra-office', N'Opposite NADRA Office, Pasrur, District Sialkot', N'0336-0112210', 1),
  (187, N'Kapoorwali Campus', N'kapoorwali-campus', N'Kapoorwali', N'kapoorwali', N'Sialkot', N'sialkot', N'Punjab', N'punjab', N'Kapoorwali', N'kapoorwali', N'Kapoorwali, District Sialkot', N'0300-6166919', 1);

-- Regions
INSERT INTO dbo.regions (name, slug, sort_order)
SELECT DISTINCT x.region, x.region_slug, 0
FROM @c x
WHERE NOT EXISTS (SELECT 1 FROM dbo.regions r WHERE r.slug = x.region_slug);

-- Districts
INSERT INTO dbo.districts (region_id, name, slug, sort_order)
SELECT r.id, d.district, d.district_slug, 0
FROM (SELECT DISTINCT district, district_slug, region_slug FROM @c) d
JOIN dbo.regions r ON r.slug = d.region_slug
WHERE NOT EXISTS (SELECT 1 FROM dbo.districts x WHERE x.slug = d.district_slug);

-- Cities (an existing city, such as Faisalabad or Chiniot, is used as it is)
INSERT INTO dbo.cities (region_id, district_id, name, slug, sort_order)
SELECT d.region_id, d.id, c.city, c.city_slug, 0
FROM (SELECT DISTINCT city, city_slug, district_slug FROM @c) c
JOIN dbo.districts d ON d.slug = c.district_slug
WHERE NOT EXISTS (SELECT 1 FROM dbo.cities x WHERE x.slug = c.city_slug OR x.name = c.city);

-- Campuses
INSERT INTO dbo.branches (city_id, name, slug, area, area_slug, address_line, phone, status,
                          is_published, published_at, admission_status, sort_order)
SELECT ci.id, c.name, c.slug, c.area, c.area_slug, c.address, c.phone, N'active',
       c.published, CASE WHEN c.published = 1 THEN SYSDATETIMEOFFSET() END, N'contact_campus', 300 + c.row_no
FROM @c c
CROSS APPLY (SELECT TOP 1 x.id FROM dbo.cities x WHERE x.slug = c.city_slug OR x.name = c.city
             ORDER BY CASE WHEN x.slug = c.city_slug THEN 0 ELSE 1 END, x.created_at) ci
WHERE NOT EXISTS (SELECT 1 FROM dbo.branches b WHERE b.city_id = ci.id AND (b.slug = c.slug OR b.name = c.name) AND b.deleted_at IS NULL);

INSERT INTO dbo.schema_migrations (id) VALUES (N'20_flyer_campuses');
COMMIT TRANSACTION;
PRINT N'20_flyer_campuses applied.';
GO
SET NOEXEC OFF;
GO
