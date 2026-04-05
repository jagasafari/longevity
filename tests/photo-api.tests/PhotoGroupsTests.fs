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

        testCase "moves source photo to target group when in distinct groups" <| fun () ->
            let result =
                PhotoGroups.planGroupChange
                    (Some "group-a")
                    (Some "group-b")
                    "a.jpg"
                    "b.jpg"
            test <@ result = PhotoGroups.MoveToGroup ("group-a", "group-b", "a.jpg") @>

        testCase "creates subgroup when both photos are in same group" <| fun () ->
            let result =
                PhotoGroups.planGroupChange
                    (Some "group-a")
                    (Some "group-a")
                    "a.jpg"
                    "b.jpg"
            test <@ result = PhotoGroups.CreateSubgroup ("group-a", "a.jpg", "b.jpg") @>
    ]

    testList "classifyGroup" [

        testCase "empty group with no names or children" <| fun () ->
            test <@ PhotoGroups.classifyGroup 0 0 0 = PhotoGroups.Empty @>

        testCase "singleton group" <| fun () ->
            test <@ PhotoGroups.classifyGroup 1 0 0 = PhotoGroups.Singleton @>

        testCase "group with names" <| fun () ->
            test <@ PhotoGroups.classifyGroup 0 0 1 = PhotoGroups.HasNames @>

        testCase "group with children" <| fun () ->
            test <@ PhotoGroups.classifyGroup 1 1 0 = PhotoGroups.HasChildren @>

        testCase "healthy group with multiple photos" <| fun () ->
            test <@ PhotoGroups.classifyGroup 2 0 0 = PhotoGroups.Healthy @>
    ]

    testList "decideCleanup" [

        testCase "deletes empty groups" <| fun () ->
            test <@ PhotoGroups.decideCleanup PhotoGroups.Empty = PhotoGroups.DeleteGroup @>

        testCase "deletes singleton groups" <| fun () ->
            test <@ PhotoGroups.decideCleanup PhotoGroups.Singleton = PhotoGroups.DeleteGroup @>

        testCase "keeps groups with names" <| fun () ->
            test <@ PhotoGroups.decideCleanup PhotoGroups.HasNames = PhotoGroups.KeepGroup @>

        testCase "keeps groups with children" <| fun () ->
            test <@ PhotoGroups.decideCleanup PhotoGroups.HasChildren = PhotoGroups.KeepGroup @>

        testCase "keeps healthy groups" <| fun () ->
            test <@ PhotoGroups.decideCleanup PhotoGroups.Healthy = PhotoGroups.KeepGroup @>
    ]

    testList "planMove" [

        testCase "already in target group" <| fun () ->
            test <@ PhotoGroups.planMove (Some "g1") "g1" = PhotoGroups.AlreadyInTarget @>

        testCase "move from one group to another" <| fun () ->
            test <@ PhotoGroups.planMove (Some "g1") "g2" = PhotoGroups.MoveFromGroup "g1" @>

        testCase "add ungrouped photo to group" <| fun () ->
            test <@ PhotoGroups.planMove None "g1" = PhotoGroups.AddToGroup @>
    ]
]