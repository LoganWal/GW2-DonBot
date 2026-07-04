type JsonEventHandler<T = unknown> = (payload: T, event: MessageEvent) => void | Promise<void>
type RawEventHandler = (event: MessageEvent) => void | Promise<void>

type EventSourceOptions = {
  withCredentials?: boolean
  handlers?: Record<string, RawEventHandler>
  jsonHandlers?: Record<string, JsonEventHandler>
  message?: JsonEventHandler
  onParseError?: (error: unknown, eventName: string, event: MessageEvent) => void
  onError?: (event: Event, close: () => void) => void
  closeOnError?: boolean
}

export const useEventSource = () => {
  const sources = new Set<EventSource>()

  const close = (source: EventSource) => {
    source.close()
    sources.delete(source)
  }

  const openEventSource = (url: string, options: EventSourceOptions = {}) => {
    if (typeof EventSource === 'undefined') {
      return null
    }

    const source = new EventSource(url, { withCredentials: options.withCredentials ?? true })
    sources.add(source)

    const closeSource = () => close(source)
    const onParseError = options.onParseError ?? ((error, eventName) => {
      console.warn(`event-source: failed to parse ${eventName} event`, error)
    })

    if (options.message) {
      source.onmessage = event => handleJsonEvent(event, 'message', options.message!, onParseError)
    }

    for (const [eventName, handler] of Object.entries(options.jsonHandlers ?? {})) {
      source.addEventListener(eventName, event => {
        handleJsonEvent(event as MessageEvent, eventName, handler, onParseError)
      })
    }

    for (const [eventName, handler] of Object.entries(options.handlers ?? {})) {
      source.addEventListener(eventName, event => {
        handler(event as MessageEvent)
      })
    }

    source.onerror = event => {
      if (options.onError) {
        options.onError(event, closeSource)
        return
      }
      if (options.closeOnError) {
        closeSource()
      }
    }

    return { source, close: closeSource }
  }

  const closeAll = () => {
    for (const source of [...sources]) {
      close(source)
    }
  }

  onUnmounted(closeAll)

  return { openEventSource, closeAll }
}

const handleJsonEvent = async (
  event: MessageEvent,
  eventName: string,
  handler: JsonEventHandler,
  onParseError: NonNullable<EventSourceOptions['onParseError']>
) => {
  try {
    await handler(JSON.parse(event.data), event)
  } catch (error) {
    onParseError(error, eventName, event)
  }
}
