--
-- PostgreSQL database dump
--

\restrict O04wkRpE6boIr1RbG8E5lMGXTXj7YY44wiOzmduneLZCpjbTUmDJFbOTu3M5xw4

-- Dumped from database version 16.14
-- Dumped by pg_dump version 16.14

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: vocabulary; Type: SCHEMA; Schema: -; Owner: longevity
--

CREATE SCHEMA vocabulary;


ALTER SCHEMA vocabulary OWNER TO longevity;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: categories; Type: TABLE; Schema: public; Owner: longevity
--

CREATE TABLE public.categories (
    id integer NOT NULL,
    name text NOT NULL,
    CONSTRAINT categories_name_not_blank CHECK ((name <> ''::text))
);


ALTER TABLE public.categories OWNER TO longevity;

--
-- Name: categories_id_seq; Type: SEQUENCE; Schema: public; Owner: longevity
--

CREATE SEQUENCE public.categories_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.categories_id_seq OWNER TO longevity;

--
-- Name: categories_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: longevity
--

ALTER SEQUENCE public.categories_id_seq OWNED BY public.categories.id;


--
-- Name: photo_group_categories; Type: TABLE; Schema: public; Owner: longevity
--

CREATE TABLE public.photo_group_categories (
    group_id text NOT NULL,
    category_id integer NOT NULL
);


ALTER TABLE public.photo_group_categories OWNER TO longevity;

--
-- Name: photo_group_members; Type: TABLE; Schema: public; Owner: longevity
--

CREATE TABLE public.photo_group_members (
    group_id text NOT NULL,
    photo_name text NOT NULL,
    CONSTRAINT group_photos_name_not_blank CHECK ((photo_name <> ''::text))
);


ALTER TABLE public.photo_group_members OWNER TO longevity;

--
-- Name: photo_groups; Type: TABLE; Schema: public; Owner: longevity
--

CREATE TABLE public.photo_groups (
    group_id text NOT NULL,
    parent_group_id text,
    CONSTRAINT photo_groups_id_not_blank CHECK ((group_id <> ''::text)),
    CONSTRAINT photo_groups_no_self_parent CHECK (((parent_group_id IS NULL) OR (parent_group_id <> group_id)))
);


ALTER TABLE public.photo_groups OWNER TO longevity;

--
-- Name: schemaversions; Type: TABLE; Schema: public; Owner: longevity
--

CREATE TABLE public.schemaversions (
    schemaversionsid integer NOT NULL,
    scriptname character varying(255) NOT NULL,
    applied timestamp without time zone NOT NULL
);


ALTER TABLE public.schemaversions OWNER TO longevity;

--
-- Name: schemaversions_schemaversionsid_seq; Type: SEQUENCE; Schema: public; Owner: longevity
--

CREATE SEQUENCE public.schemaversions_schemaversionsid_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.schemaversions_schemaversionsid_seq OWNER TO longevity;

--
-- Name: schemaversions_schemaversionsid_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: longevity
--

ALTER SEQUENCE public.schemaversions_schemaversionsid_seq OWNED BY public.schemaversions.schemaversionsid;


--
-- Name: groups; Type: TABLE; Schema: vocabulary; Owner: longevity
--

CREATE TABLE vocabulary.groups (
    id text NOT NULL,
    name text NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT vocabulary_groups_id_not_blank CHECK ((id <> ''::text)),
    CONSTRAINT vocabulary_groups_name_not_blank CHECK ((name <> ''::text))
);


ALTER TABLE vocabulary.groups OWNER TO longevity;

--
-- Name: photos; Type: TABLE; Schema: vocabulary; Owner: longevity
--

CREATE TABLE vocabulary.photos (
    photo_name text NOT NULL,
    group_id text NOT NULL,
    added_at timestamp with time zone DEFAULT now() NOT NULL,
    subgroup_id text,
    subgroup_word text,
    removed_at timestamp with time zone,
    word text,
    source text,
    confidence real,
    labeled_at timestamp with time zone,
    labeled_by text,
    CONSTRAINT vocabulary_photos_name_not_blank CHECK ((photo_name <> ''::text))
);


ALTER TABLE vocabulary.photos OWNER TO longevity;

--
-- Name: categories id; Type: DEFAULT; Schema: public; Owner: longevity
--

ALTER TABLE ONLY public.categories ALTER COLUMN id SET DEFAULT nextval('public.categories_id_seq'::regclass);


--
-- Name: schemaversions schemaversionsid; Type: DEFAULT; Schema: public; Owner: longevity
--

ALTER TABLE ONLY public.schemaversions ALTER COLUMN schemaversionsid SET DEFAULT nextval('public.schemaversions_schemaversionsid_seq'::regclass);


--
-- Data for Name: categories; Type: TABLE DATA; Schema: public; Owner: longevity
--

COPY public.categories (id, name) FROM stdin;
1	heartbreaking
2	love&death
4	house of cards
5	detective
\.


--
-- Data for Name: photo_group_categories; Type: TABLE DATA; Schema: public; Owner: longevity
--

COPY public.photo_group_categories (group_id, category_id) FROM stdin;
\.


--
-- Data for Name: photo_group_members; Type: TABLE DATA; Schema: public; Owner: longevity
--

COPY public.photo_group_members (group_id, photo_name) FROM stdin;
c35a6cae43b2452697553be9eed293cc	20260315_104019.jpg
c35a6cae43b2452697553be9eed293cc	20260315_104014.jpg
c4b06ea9bf6c48289ba8d6aa70bbda05	20260308_074833.jpg
c4b06ea9bf6c48289ba8d6aa70bbda05	20260308_074848.jpg
74f8fb8573424b719b16596d866fc751	20260308_085224.jpg
74f8fb8573424b719b16596d866fc751	20260308_085221.jpg
0195826e160b46ceb0ff5ccba68a8909	20260312_124152.jpg
0195826e160b46ceb0ff5ccba68a8909	20260312_124200.jpg
c35a6cae43b2452697553be9eed293cc	20260315_091332.jpg
\.


--
-- Data for Name: photo_groups; Type: TABLE DATA; Schema: public; Owner: longevity
--

COPY public.photo_groups (group_id, parent_group_id) FROM stdin;
c35a6cae43b2452697553be9eed293cc	\N
c4b06ea9bf6c48289ba8d6aa70bbda05	\N
74f8fb8573424b719b16596d866fc751	\N
0195826e160b46ceb0ff5ccba68a8909	\N
\.


--
-- Data for Name: schemaversions; Type: TABLE DATA; Schema: public; Owner: longevity
--

COPY public.schemaversions (schemaversionsid, scriptname, applied) FROM stdin;
1	longevity-backend.Migrations.V001__create_group_photos.sql	2026-03-20 14:31:36.69843
2	longevity-backend.Migrations.V002__unique_photo_name.sql	2026-03-20 14:31:36.773463
3	longevity-backend.Migrations.V003__create_categories.sql	2026-03-22 11:55:23.413961
4	photo-api.Migrations.V001__create_group_photos.sql	2026-03-23 14:10:19.41158
5	photo-api.Migrations.V002__unique_photo_name.sql	2026-03-23 14:10:19.500778
6	photo-api.Migrations.V003__create_categories.sql	2026-03-23 14:13:43.567722
7	photo-api.Migrations.V004__create_photo_counts.sql	2026-03-23 14:13:43.589671
8	photo-api.Migrations.V005__create_hierarchical_groups.sql	2026-03-26 07:19:13.169135
9	photo-api.Migrations.V006__restrict_cascade_deletes.sql	2026-03-29 09:11:32.72833
10	photo-api.Migrations.V007__drop_photo_counts.sql	2026-03-29 09:49:01.925508
11	photo-api.Migrations.V008__rename_junction_tables.sql	2026-03-29 09:49:01.948095
12	photo-api.Migrations.V001__initial_schema.sql	2026-04-02 15:04:08.193123
13	photo-api.Migrations.V002__replace_categories_with_group_names.sql	2026-04-05 05:32:01.706713
14	photo-api.Migrations.V003__vocabulary_schema.sql	2026-05-30 15:10:23.074517
15	photo-api.Migrations.V004__vocabulary_groups.sql	2026-05-31 05:19:00.794705
16	photo-api.Migrations.V005__add_subgroup_to_vocabulary_photos.sql	2026-05-31 05:37:14.217377
17	photo-api.Migrations.V006__vocabulary_subgroup_word.sql	2026-05-31 07:56:07.68689
18	photo-api.Migrations.V007__vocabulary_photos_soft_delete.sql	2026-05-31 15:19:09.025467
19	photo-api.Migrations.V008__vocabulary_photo_word.sql	2026-05-31 16:07:00.909401
\.


--
-- Data for Name: groups; Type: TABLE DATA; Schema: vocabulary; Owner: longevity
--

COPY vocabulary.groups (id, name, created_at) FROM stdin;
b1789f813b0b434884d7ea44e830b979	love&death	2026-05-30 15:10:22.896864+00
e7b225f6f4054f16843a595cba4564c4	house of cards	2026-05-30 15:10:22.896864+00
dd65995f0bf1455a899b0a6939314203	detective	2026-05-30 15:10:22.896864+00
92b12080bb664899b6df6290892f769e	heartbreaking	2026-05-30 15:10:22.896864+00
14b009ced9ea44d49263ca6dfd80271a	Untitled	2026-05-30 15:10:22.896864+00
79bf829a766b47c982b8b4776d98f652	Untitled	2026-05-30 15:15:29.427126+00
4e9c16cade684be6a519d8a9a9b7adbd	Untitled	2026-05-30 21:26:59.408286+00
bf3948fd050a476da5741154b2241bff	machu pichcu	2026-05-30 21:27:01.563999+00
ed3e76be29e747c5b991a33784be6cfa	severance	2026-05-30 21:27:03.428207+00
\.


--
-- Data for Name: photos; Type: TABLE DATA; Schema: vocabulary; Owner: longevity
--

COPY vocabulary.photos (photo_name, group_id, added_at, subgroup_id, subgroup_word, removed_at, word, source, confidence, labeled_at, labeled_by) FROM stdin;
ChatGPT-Image-Feb-24-2026-07_52_13-PM.png	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	e552bc05e53244649ee2eb582cfa7b09	\N	\N	\N	\N	\N	\N	\N
20260322_211028.jpg	b1789f813b0b434884d7ea44e830b979	2026-05-30 15:10:22.896864+00	\N	\N	\N	hue	netflix_caption	1	2026-05-31 22:02:52.717199+00	ai
20260322_210224.jpg	b1789f813b0b434884d7ea44e830b979	2026-05-30 15:10:22.896864+00	\N	\N	\N	future	netflix_caption	0.9	2026-05-31 22:02:57.941455+00	ai
20260322_200319.jpg	b1789f813b0b434884d7ea44e830b979	2026-05-30 15:10:22.896864+00	\N	\N	\N	applause	netflix_caption	0.9	2026-05-31 22:03:03.131475+00	ai
20260322_201012.jpg	b1789f813b0b434884d7ea44e830b979	2026-05-30 15:10:22.896864+00	\N	\N	\N	love	netflix_caption	1	2026-05-31 22:03:08.575901+00	ai
20260322_201858.jpg	b1789f813b0b434884d7ea44e830b979	2026-05-30 15:10:22.896864+00	\N	\N	\N	vow	netflix_caption	0.9	2026-05-31 22:03:13.683523+00	ai
20260322_202722.jpg	b1789f813b0b434884d7ea44e830b979	2026-05-30 15:10:22.896864+00	\N	\N	\N	love	netflix_caption	0.9	2026-05-31 22:03:18.702492+00	ai
20260322_195947.jpg	b1789f813b0b434884d7ea44e830b979	2026-05-30 15:10:22.896864+00	\N	\N	\N	death	netflix_caption	0.9	2026-05-31 22:03:23.709447+00	ai
20260322_204615.jpg	b1789f813b0b434884d7ea44e830b979	2026-05-30 15:10:22.896864+00	\N	\N	\N	therapy	netflix_caption	0.9	2026-05-31 22:03:33.872086+00	ai
20260322_201537.jpg	b1789f813b0b434884d7ea44e830b979	2026-05-30 15:10:22.896864+00	\N	\N	\N	fornicate	netflix_caption	0.9	2026-05-31 22:03:38.831376+00	ai
20260322_195738.jpg	b1789f813b0b434884d7ea44e830b979	2026-05-30 15:10:22.896864+00	\N	\N	\N	death	netflix_caption	0.9	2026-05-31 22:03:44.025889+00	ai
20260322_205449.jpg	b1789f813b0b434884d7ea44e830b979	2026-05-30 15:10:22.896864+00	\N	\N	\N	parables	netflix_caption	0.9	2026-05-31 22:03:49.329949+00	ai
20260322_210805.jpg	b1789f813b0b434884d7ea44e830b979	2026-05-30 15:10:22.896864+00	\N	\N	\N	sky	netflix_caption	0.9	2026-05-31 22:03:54.412963+00	ai
20260322_210435.jpg	b1789f813b0b434884d7ea44e830b979	2026-05-30 15:10:22.896864+00	\N	\N	\N	couples	netflix_caption	0.9	2026-05-31 22:04:00.192543+00	ai
20260322_211001.jpg	b1789f813b0b434884d7ea44e830b979	2026-05-30 15:10:22.896864+00	\N	\N	\N	tapestry	netflix_caption	0.9	2026-05-31 22:04:05.160715+00	ai
Understanding-the-word-goofy.png	79bf829a766b47c982b8b4776d98f652	2026-05-30 15:15:29.427126+00	\N	\N	\N	goofy	netflix_caption	1	2026-05-31 22:07:10.469341+00	ai
b6W38ElSF-uSC8SdcBoZbRb_KOoi-G73Z0pMGqN0qe3s5DSaSsb6FYdI1zzR-mkJB54NFQ8-yWqqowvs9vX4f9SHgjsK0oTKQR8KnC7BjKGzLNMzj937Q4gp6ZW5r_pCyhPuUqwefzSiL9wAuahp97ykksirvffUUAfrCOd3H0slGtwhwuHjR3QkcTrVBWhO.jpeg	79bf829a766b47c982b8b4776d98f652	2026-05-30 15:15:29.427126+00	\N	\N	\N	freedom	object	0.9	2026-05-31 22:07:13.661938+00	ai
20260401_160652.jpg	92b12080bb664899b6df6290892f769e	2026-05-30 15:10:22.896864+00	\N	\N	\N	\N	\N	\N	\N	\N
20260401_155546.jpg	92b12080bb664899b6df6290892f769e	2026-05-30 15:10:22.896864+00	\N	\N	\N	\N	\N	\N	\N	\N
20260401_155633.jpg	92b12080bb664899b6df6290892f769e	2026-05-30 15:10:22.896864+00	\N	\N	\N	\N	\N	\N	\N	\N
20260401_155837.jpg	92b12080bb664899b6df6290892f769e	2026-05-30 15:10:22.896864+00	\N	\N	\N	\N	\N	\N	\N	\N
YJ7CbtlPEBPvf_AUkzMAdtoz9Qz1KHaNuUbkpBWnOK5ZuySz_t9QguLjtJCAB7FJAu00miytn72VUO4RICKBWRMX-imjuRqcVogIAGa9QY48v0v_053Gu_ZvChE9wSoQGmsoKyOuVSCnN3YePzDg0kHoOq6xjFEbhLgFhoSlAE3vrWb_GsDqQG1-OOsbbDhS.jpeg	79bf829a766b47c982b8b4776d98f652	2026-05-30 15:15:29.427126+00	\N	\N	\N	rope	object	0.95	2026-05-31 22:07:17.221455+00	ai
RUOcXPvw616WBfKEosIkztrIFvrHJYTPuvGLs277uym2lVE2HjnU_IN_dlHpG-35RlAyrQ8QI2c2_Pa49c1hLgDGKqZ_xeLtBYt0OhLysylx2TLYDghDhpBJYTXSBA3wq7WCMlS_eBLsiBJhnt29mzuyoTb5zLRjI1uetNx4vxkQMGfFI96-rYo0bwQczd8T.jpeg	79bf829a766b47c982b8b4776d98f652	2026-05-30 15:15:29.427126+00	\N	\N	\N	cage	object	0.9	2026-05-31 22:07:20.026406+00	ai
20260322_204124.jpg	b1789f813b0b434884d7ea44e830b979	2026-05-30 15:10:22.896864+00	\N	\N	\N	feelings	netflix_caption	0.9	2026-05-31 22:03:28.763547+00	ai
20260329_220713.jpg	dd65995f0bf1455a899b0a6939314203	2026-05-30 15:10:22.896864+00	\N	\N	\N	\N	\N	\N	\N	\N
20260329_222629.jpg	dd65995f0bf1455a899b0a6939314203	2026-05-30 15:10:22.896864+00	\N	\N	\N	\N	\N	\N	\N	\N
20260330_175015.jpg	dd65995f0bf1455a899b0a6939314203	2026-05-30 15:10:22.896864+00	\N	\N	\N	\N	\N	\N	\N	\N
20260330_175204.jpg	dd65995f0bf1455a899b0a6939314203	2026-05-30 15:10:22.896864+00	\N	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Mar-29-2026-06_19_22-PM.png	b1789f813b0b434884d7ea44e830b979	2026-05-30 15:10:22.896864+00	\N	\N	\N	straightest	netflix_caption	0.9	2026-05-31 22:05:21.897728+00	ai
ChatGPT-Image-Mar-29-2026-06_19_36-PM.png	b1789f813b0b434884d7ea44e830b979	2026-05-30 15:15:29.427126+00	\N	\N	\N	parable	netflix_caption	0.9	2026-05-31 22:05:32.955297+00	ai
ChatGPT-Image-Mar-29-2026-06_20_42-PM.png	b1789f813b0b434884d7ea44e830b979	2026-05-30 15:15:29.427126+00	\N	\N	\N	feign	netflix_caption	1	2026-05-31 22:06:54.051391+00	ai
ChatGPT-Image-Mar-29-2026-06_20_45-PM.png	79bf829a766b47c982b8b4776d98f652	2026-05-30 15:15:29.427126+00	5da34e02fc5343e18c1ae0a54fbc2583	\N	\N	test_word	object	0.9	2026-05-31 19:37:27.551381+00	ai
ChatGPT-Image-Mar-29-2026-06_19_17-PM.png	14b009ced9ea44d49263ca6dfd80271a	2026-05-30 15:10:22.896864+00	0ed9b8762b78419ebda22029c5237e9e	\N	\N	create	ai_image_with_word	0.9	2026-05-31 22:07:03.783422+00	ai
ChatGPT-Image-Feb-24-2026-07_43_27-PM.png	4e9c16cade684be6a519d8a9a9b7adbd	2026-05-30 21:26:59.408286+00	1b2f5038097c407bb1f7e35a74f6e330	\N	\N	dubious	netflix_caption	1	2026-05-31 22:07:35.400902+00	ai
ChatGPT-Image-Mar-29-2026-06_19_27-PM.png	79bf829a766b47c982b8b4776d98f652	2026-05-30 15:15:29.427126+00	e748ccc54def4590b0ab74c0d7e2938e	\N	\N	leadership	object	0.9	2026-05-31 22:07:47.130226+00	ai
ChatGPT-Image-Feb-24-2026-07_41_41-PM.png	bf3948fd050a476da5741154b2241bff	2026-05-30 21:27:01.563999+00	3a76ca5a518d476c90b73b4aeede7e6a	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_41_44-PM.png	bf3948fd050a476da5741154b2241bff	2026-05-30 21:27:01.563999+00	3a76ca5a518d476c90b73b4aeede7e6a	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_41_51-PM.png	bf3948fd050a476da5741154b2241bff	2026-05-30 21:27:01.563999+00	ed9a0de503db46ae8199a5b18bd7c3c8	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_42_33-PM.png	bf3948fd050a476da5741154b2241bff	2026-05-30 21:27:01.563999+00	ed9a0de503db46ae8199a5b18bd7c3c8	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Mar-29-2026-06_21_03-PM.png	79bf829a766b47c982b8b4776d98f652	2026-05-30 15:15:29.427126+00	033f62161d3b4e2f96f6b3cf734885a4	\N	\N	trophy	object	0.9	2026-05-31 22:08:08.977458+00	ai
ChatGPT-Image-Mar-29-2026-06_21_07-PM.png	79bf829a766b47c982b8b4776d98f652	2026-05-30 15:15:29.427126+00	033f62161d3b4e2f96f6b3cf734885a4	\N	\N	turmoil	ai_image_with_word	1	2026-05-31 22:08:20.12094+00	ai
ChatGPT-Image-Mar-29-2026-06_20_54-PM.png	b1789f813b0b434884d7ea44e830b979	2026-05-30 15:15:29.427126+00	\N	\N	\N	squelch	netflix_caption	1	2026-05-31 22:02:31.268965+00	ai
ChatGPT-Image-Mar-29-2026-06_20_29-PM.png	b1789f813b0b434884d7ea44e830b979	2026-05-30 15:15:29.427126+00	\N	\N	\N	honest	netflix_caption	0.9	2026-05-31 22:04:15.747506+00	ai
ChatGPT-Image-Mar-29-2026-06_20_50-PM.png	b1789f813b0b434884d7ea44e830b979	2026-05-30 15:15:29.427126+00	\N	\N	\N	applaud	netflix_caption	1	2026-05-31 22:04:26.412033+00	ai
ChatGPT-Image-Mar-29-2026-06_20_36-PM.png	b1789f813b0b434884d7ea44e830b979	2026-05-30 15:15:29.427126+00	\N	\N	\N	fornicate	netflix_caption	0.9	2026-05-31 22:04:48.551868+00	ai
ChatGPT-Image-Mar-29-2026-06_18_27-PM.png	b1789f813b0b434884d7ea44e830b979	2026-05-30 15:15:29.427126+00	\N	\N	\N	hue	netflix_caption	1	2026-05-31 22:05:00.024051+00	ai
ChatGPT-Image-Mar-29-2026-06_20_58-PM.png	b1789f813b0b434884d7ea44e830b979	2026-05-30 15:15:29.427126+00	\N	\N	\N	rejected	netflix_caption	0.9	2026-05-31 22:05:11.131689+00	ai
ChatGPT-Image-Feb-24-2026-07_43_44-PM.png	4e9c16cade684be6a519d8a9a9b7adbd	2026-05-30 21:26:59.408286+00	1b2f5038097c407bb1f7e35a74f6e330	\N	\N	detention	netflix_caption	0.9	2026-05-31 22:08:43.571102+00	ai
ChatGPT-Image-Mar-29-2026-06_18_43-PM.png	b1789f813b0b434884d7ea44e830b979	2026-05-30 15:15:29.427126+00	\N	\N	\N	tapestry	netflix_caption	1	2026-05-31 22:07:36.173155+00	ai
ChatGPT-Image-Mar-29-2026-06_19_12-PM.png	b1789f813b0b434884d7ea44e830b979	2026-05-30 15:15:29.427126+00	\N	\N	\N	tumbling	netflix_caption	0.9	2026-05-31 22:07:25.434341+00	ai
ChatGPT-Image-Mar-29-2026-06_19_31-PM.png	b1789f813b0b434884d7ea44e830b979	2026-05-30 15:15:29.427126+00	\N	\N	\N	cleaning	object	0.9	2026-05-31 22:07:30.464353+00	ai
ChatGPT-Image-Mar-29-2026-06_20_32-PM.png	b1789f813b0b434884d7ea44e830b979	2026-05-30 15:15:29.427126+00	\N	\N	\N	vow	netflix_caption	0.9	2026-05-31 22:07:58.101229+00	ai
Screenshot_20260224_194838_Chrome.jpg	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	f8bd9212922e40a5adcf5d2839614d6f	\N	\N	\N	\N	\N	\N	\N
Screenshot_20260224_194902_Chrome.jpg	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	fa1883168b294aba86307f5cd099a037	\N	\N	\N	\N	\N	\N	\N
Screenshot_20260224_194924_Chrome.jpg	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	e98e27080f694e3c8c34bb750128ee5a	\N	\N	\N	\N	\N	\N	\N
Screenshot_20260224_194953_Chrome.jpg	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	c2b788f289c049bab4f02e7f67ece463	\N	\N	\N	\N	\N	\N	\N
Screenshot_20260224_195010_Chrome.jpg	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	3d2bcae268224dc7bfb89fda48b6fe57	\N	\N	\N	\N	\N	\N	\N
Screenshot_20260224_195030_Chrome.jpg	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	e855a49de9d145c587704edb6264fff4	\N	\N	\N	\N	\N	\N	\N
Screenshot_20260224_195047_Chrome.jpg	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	e7ec87f5f0ff4091a1bc66d911ef8dac	\N	\N	\N	\N	\N	\N	\N
Screenshot_20260224_195102_Chrome.jpg	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	8babca4c28024756bda57d0b4e329d77	\N	\N	\N	\N	\N	\N	\N
Screenshot_20260224_195119_Chrome.jpg	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	aafa77af73c94c4d890cf3bc266b2bdd	\N	\N	\N	\N	\N	\N	\N
Screenshot_20260224_195137_Chrome.jpg	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	04b71709ae914dc088cb60d0cdb0f026	\N	\N	\N	\N	\N	\N	\N
Screenshot_20260224_195153_Chrome.jpg	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	0fd84264ad0d49288e1ce452dd74814f	\N	\N	\N	\N	\N	\N	\N
Screenshot_20260224_195219_Chrome.jpg	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	e552bc05e53244649ee2eb582cfa7b09	\N	\N	\N	\N	\N	\N	\N
Screenshot_20260224_195233_Chrome.jpg	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	6de971b5d6ea40bcab9e4c4ad85804eb	\N	\N	\N	\N	\N	\N	\N
Screenshot_20260224_195248_Chrome.jpg	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	ff4fa7b615944d8b891c10314acaca3b	\N	\N	\N	\N	\N	\N	\N
Screenshot_20260224_195301_Chrome.jpg	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	c31f234013314f9392088a0f25b8de38	\N	\N	\N	\N	\N	\N	\N
Screenshot_20260224_195317_Chrome.jpg	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	235f30538c744a61a52402fe5891cd87	\N	\N	\N	\N	\N	\N	\N
Screenshot_20260224_195339_Chrome.jpg	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	a7fd0acd9a39497cb4b205fa1ed2c99e	\N	\N	\N	\N	\N	\N	\N
Screenshot_20260224_195400_Chrome.jpg	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	c25a24d419cb44daa17e36d34d4e4195	\N	\N	\N	\N	\N	\N	\N
Screenshot_20260224_195419_Chrome.jpg	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	068b77f680f249288ca82c52c393fdc8	\N	\N	\N	\N	\N	\N	\N
Screenshot_20260224_195450_Chrome.jpg	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	782f50eba740445fb3499d975ce7401b	\N	\N	\N	\N	\N	\N	\N
Screenshot_20260224_195459_Chrome.jpg	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	782f50eba740445fb3499d975ce7401b	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_39_33-PM.png	ed3e76be29e747c5b991a33784be6cfa	2026-05-30 21:27:03.428207+00	a0fd002481354ab284f661a4a6001827	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_42_40-PM.png	bf3948fd050a476da5741154b2241bff	2026-05-30 21:27:01.563999+00	29fe2a66eb05464f824e3880d8774065	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_42_51-PM.png	bf3948fd050a476da5741154b2241bff	2026-05-30 21:27:01.563999+00	29fe2a66eb05464f824e3880d8774065	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_48_28-PM.png	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	f8bd9212922e40a5adcf5d2839614d6f	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_48_48-PM.png	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	e685d77de31b41f49007b50d257ecbdc	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_48_54-PM.png	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	e685d77de31b41f49007b50d257ecbdc	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_49_11-PM.png	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	fa1883168b294aba86307f5cd099a037	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_49_17-PM.png	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	e98e27080f694e3c8c34bb750128ee5a	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_49_39-PM.png	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	c2b788f289c049bab4f02e7f67ece463	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_50_19-PM.png	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	3d2bcae268224dc7bfb89fda48b6fe57	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_50_39-PM.png	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	e855a49de9d145c587704edb6264fff4	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_50_56-PM.png	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	e7ec87f5f0ff4091a1bc66d911ef8dac	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_51_12-PM.png	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	8babca4c28024756bda57d0b4e329d77	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_51_28-PM.png	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	aafa77af73c94c4d890cf3bc266b2bdd	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_51_46-PM.png	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	04b71709ae914dc088cb60d0cdb0f026	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_52_00-PM.png	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	0fd84264ad0d49288e1ce452dd74814f	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_52_06-PM.png	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	b2aadfe9aaa94452a1e19cb5dbedd420	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_52_09-PM.png	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	b2aadfe9aaa94452a1e19cb5dbedd420	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_52_27-PM.png	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	6de971b5d6ea40bcab9e4c4ad85804eb	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_52_42-PM.png	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	ff4fa7b615944d8b891c10314acaca3b	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_52_55-PM.png	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	c31f234013314f9392088a0f25b8de38	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_53_10-PM.png	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	235f30538c744a61a52402fe5891cd87	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_53_27-PM.png	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	3bf875d2c013455ea1dd459ed0cda960	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_53_31-PM.png	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	3bf875d2c013455ea1dd459ed0cda960	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_53_48-PM.png	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	a7fd0acd9a39497cb4b205fa1ed2c99e	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_54_08-PM.png	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	c25a24d419cb44daa17e36d34d4e4195	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_54_14-PM.png	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	068b77f680f249288ca82c52c393fdc8	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_54_28-PM.png	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	eabd5130c35147e2839cb556f9693092	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_54_43-PM.png	e7b225f6f4054f16843a595cba4564c4	2026-05-30 15:10:22.896864+00	eabd5130c35147e2839cb556f9693092	\N	\N	\N	\N	\N	\N	\N
ChatGPT-Image-Feb-24-2026-07_40_39-PM.png	ed3e76be29e747c5b991a33784be6cfa	2026-05-30 21:27:03.428207+00	a0fd002481354ab284f661a4a6001827	\N	\N	\N	\N	\N	\N	\N
\.


--
-- Name: categories_id_seq; Type: SEQUENCE SET; Schema: public; Owner: longevity
--

SELECT pg_catalog.setval('public.categories_id_seq', 6, true);


--
-- Name: schemaversions_schemaversionsid_seq; Type: SEQUENCE SET; Schema: public; Owner: longevity
--

SELECT pg_catalog.setval('public.schemaversions_schemaversionsid_seq', 19, true);


--
-- Name: schemaversions PK_schemaversions_Id; Type: CONSTRAINT; Schema: public; Owner: longevity
--

ALTER TABLE ONLY public.schemaversions
    ADD CONSTRAINT "PK_schemaversions_Id" PRIMARY KEY (schemaversionsid);


--
-- Name: categories categories_name_key; Type: CONSTRAINT; Schema: public; Owner: longevity
--

ALTER TABLE ONLY public.categories
    ADD CONSTRAINT categories_name_key UNIQUE (name);


--
-- Name: categories categories_pkey; Type: CONSTRAINT; Schema: public; Owner: longevity
--

ALTER TABLE ONLY public.categories
    ADD CONSTRAINT categories_pkey PRIMARY KEY (id);


--
-- Name: photo_group_categories photo_group_categories_pkey; Type: CONSTRAINT; Schema: public; Owner: longevity
--

ALTER TABLE ONLY public.photo_group_categories
    ADD CONSTRAINT photo_group_categories_pkey PRIMARY KEY (group_id, category_id);


--
-- Name: photo_group_members photo_group_members_photo_name_key; Type: CONSTRAINT; Schema: public; Owner: longevity
--

ALTER TABLE ONLY public.photo_group_members
    ADD CONSTRAINT photo_group_members_photo_name_key UNIQUE (photo_name);


--
-- Name: photo_group_members photo_group_members_pkey; Type: CONSTRAINT; Schema: public; Owner: longevity
--

ALTER TABLE ONLY public.photo_group_members
    ADD CONSTRAINT photo_group_members_pkey PRIMARY KEY (group_id, photo_name);


--
-- Name: photo_groups photo_groups_pkey; Type: CONSTRAINT; Schema: public; Owner: longevity
--

ALTER TABLE ONLY public.photo_groups
    ADD CONSTRAINT photo_groups_pkey PRIMARY KEY (group_id);


--
-- Name: groups groups_pkey; Type: CONSTRAINT; Schema: vocabulary; Owner: longevity
--

ALTER TABLE ONLY vocabulary.groups
    ADD CONSTRAINT groups_pkey PRIMARY KEY (id);


--
-- Name: photos photos_pkey; Type: CONSTRAINT; Schema: vocabulary; Owner: longevity
--

ALTER TABLE ONLY vocabulary.photos
    ADD CONSTRAINT photos_pkey PRIMARY KEY (photo_name);


--
-- Name: idx_photo_group_categories_category; Type: INDEX; Schema: public; Owner: longevity
--

CREATE INDEX idx_photo_group_categories_category ON public.photo_group_categories USING btree (category_id);


--
-- Name: idx_photo_group_members_photo; Type: INDEX; Schema: public; Owner: longevity
--

CREATE INDEX idx_photo_group_members_photo ON public.photo_group_members USING btree (photo_name);


--
-- Name: idx_photo_groups_parent; Type: INDEX; Schema: public; Owner: longevity
--

CREATE INDEX idx_photo_groups_parent ON public.photo_groups USING btree (parent_group_id);


--
-- Name: idx_vocabulary_photos_group; Type: INDEX; Schema: vocabulary; Owner: longevity
--

CREATE INDEX idx_vocabulary_photos_group ON vocabulary.photos USING btree (group_id);


--
-- Name: idx_vocabulary_photos_subgroup; Type: INDEX; Schema: vocabulary; Owner: longevity
--

CREATE INDEX idx_vocabulary_photos_subgroup ON vocabulary.photos USING btree (subgroup_id) WHERE (subgroup_id IS NOT NULL);


--
-- Name: photo_group_categories photo_group_categories_category_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: longevity
--

ALTER TABLE ONLY public.photo_group_categories
    ADD CONSTRAINT photo_group_categories_category_id_fkey FOREIGN KEY (category_id) REFERENCES public.categories(id) ON DELETE CASCADE;


--
-- Name: photo_group_categories photo_group_categories_group_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: longevity
--

ALTER TABLE ONLY public.photo_group_categories
    ADD CONSTRAINT photo_group_categories_group_id_fkey FOREIGN KEY (group_id) REFERENCES public.photo_groups(group_id) ON DELETE RESTRICT;


--
-- Name: photo_group_members photo_group_members_group_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: longevity
--

ALTER TABLE ONLY public.photo_group_members
    ADD CONSTRAINT photo_group_members_group_id_fkey FOREIGN KEY (group_id) REFERENCES public.photo_groups(group_id) ON DELETE RESTRICT;


--
-- Name: photo_groups photo_groups_parent_group_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: longevity
--

ALTER TABLE ONLY public.photo_groups
    ADD CONSTRAINT photo_groups_parent_group_id_fkey FOREIGN KEY (parent_group_id) REFERENCES public.photo_groups(group_id) ON DELETE SET NULL;


--
-- Name: photos photos_group_id_fkey; Type: FK CONSTRAINT; Schema: vocabulary; Owner: longevity
--

ALTER TABLE ONLY vocabulary.photos
    ADD CONSTRAINT photos_group_id_fkey FOREIGN KEY (group_id) REFERENCES vocabulary.groups(id) ON DELETE RESTRICT;


--
-- PostgreSQL database dump complete
--

\unrestrict O04wkRpE6boIr1RbG8E5lMGXTXj7YY44wiOzmduneLZCpjbTUmDJFbOTu3M5xw4

