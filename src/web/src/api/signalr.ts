import { useEffect, useRef } from 'react'
import * as signalR from '@microsoft/signalr'
import type { LabelResult } from './schemas'

export function usePhotosHub(onPhotosChanged: () => void): void {
  const callbackRef = useRef(onPhotosChanged)
  useEffect(() => {
    callbackRef.current = onPhotosChanged
  })

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/photos')
      .withAutomaticReconnect()
      .build()

    connection.on('PhotosChanged', () => callbackRef.current())
    connection.start().catch((err) => {
      console.warn('[signalr] connection failed', err)
    })

    return () => {
      void connection.stop()
    }
  }, [])
}

export type LabelEvent =
  | { kind: 'labeled'; result: LabelResult }
  | { kind: 'failed'; photoName: string; error: string }

export function useLabelStream(onEvent: (e: LabelEvent) => void): void {
  const callbackRef = useRef(onEvent)
  useEffect(() => { callbackRef.current = onEvent })

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/photos')
      .withAutomaticReconnect()
      .build()

    connection.on('PhotoLabeled', (result: LabelResult) =>
      callbackRef.current({ kind: 'labeled', result }))
    connection.on('PhotoLabelFailed', (payload: { photoName: string; error: string }) =>
      callbackRef.current({ kind: 'failed', photoName: payload.photoName, error: payload.error }))
    connection.start().catch((err) => {
      console.warn('[signalr] label stream connection failed', err)
    })

    return () => { void connection.stop() }
  }, [])
}
