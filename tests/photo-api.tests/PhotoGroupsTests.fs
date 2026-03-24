module PhotoGroupsTests

open Expecto
open Swensen.Unquote

[<Tests>]
let tests = testList "PhotoGroups" [

    testList "planGroupChange" [

        testCase "creates new group when both photos are ungrouped" <| fun () ->
            let result = PhotoGroups.planGroupChange None None "a.jpg" "b.jpg"
            test <@ result = PhotoGroups.CreateGroup ("a.jpg", "b.jpg") @>

        testCase "adds target photo to source group" <| fun () ->
            let result =
                PhotoGroups.planGroupChange (Some "group-a") None "a.jpg" "b.jpg"
            test <@ result = PhotoGroups.AddPhotoToGroup ("group-a", "b.jpg") @>

        testCase "adds source photo to target group" <| fun () ->
            let result =
                PhotoGroups.planGroupChange None (Some "group-b") "a.jpg" "b.jpg"
            test <@ result = PhotoGroups.AddPhotoToGroup ("group-b", "a.jpg") @>

        testCase "merges distinct groups" <| fun () ->
            let result =
                PhotoGroups.planGroupChange
                    (Some "group-a")
                    (Some "group-b")
                    "a.jpg"
                    "b.jpg"
            test <@ result = PhotoGroups.MergeGroups ("group-a", "group-b") @>

        testCase "does nothing when both photos are already in same group" <| fun () ->
            let result =
                PhotoGroups.planGroupChange
                    (Some "group-a")
                    (Some "group-a")
                    "a.jpg"
                    "b.jpg"
            test <@ result = PhotoGroups.NoChange @>
    ]

    testList "shouldDeleteGroupAfterRemoval" [

        testCase "deletes empty groups" <| fun () ->
            test <@ PhotoGroups.shouldDeleteGroupAfterRemoval 0 @>

        testCase "deletes singleton groups" <| fun () ->
            test <@ PhotoGroups.shouldDeleteGroupAfterRemoval 1 @>

        testCase "keeps groups with two or more photos" <| fun () ->
            test <@ not (PhotoGroups.shouldDeleteGroupAfterRemoval 2) @>
    ]
]